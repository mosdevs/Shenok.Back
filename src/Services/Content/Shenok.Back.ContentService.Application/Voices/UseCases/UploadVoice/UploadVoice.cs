using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Voices.UseCases.UploadVoice;

public sealed record UploadVoiceRequest(Guid PageId, string Title, string ContentType, long SizeBytes, Stream Content);

public sealed record UploadVoiceResponse(
    Guid VoiceId,
    Guid PageId,
    string Title,
    string PackagingStatus,
    long SizeBytes,
    string ContentType);

public sealed class UploadVoiceValidator : AbstractValidator<UploadVoiceRequest>
{
    public UploadVoiceValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(VoiceTitle.MaxLength);
        RuleFor(x => x.ContentType)
            .Must(contentType => AudioContentTypes.TryNormalize(contentType, out _))
            .WithMessage("Source content type is not an allowed audio type.");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, AudioAsset.MaxSourceSizeBytes);
        RuleFor(x => x.Content).NotNull();
    }
}

public sealed class UploadVoiceUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IVoiceRepository voices,
    IBlobStorage blobs,
    IUnitOfWork unitOfWork) : IUseCase<UploadVoiceRequest, UploadVoiceResponse>
{
    public async Task<UploadVoiceResponse> ExecuteAsync(UploadVoiceRequest request, CancellationToken cancellationToken)
    {
        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);
        if (page.Status == PageStatus.Archived)
            throw new PageArchivedException(page.Id);

        if (request.Content is null)
            throw new InvalidAudioAssetException("Source content is required.");
        if (request.SizeBytes <= 0 || request.SizeBytes > AudioAsset.MaxSourceSizeBytes)
            throw new InvalidAudioAssetException($"Size must be greater than zero and at most {AudioAsset.MaxSourceSizeBytes} bytes.");
        if (!AudioContentTypes.TryNormalize(request.ContentType, out var contentType))
            throw new InvalidAudioAssetException("Source content type is not an allowed audio type.");
        if (request.Content.CanSeek && request.Content.Length - request.Content.Position != request.SizeBytes)
            throw new InvalidAudioAssetException("Declared size does not match the uploaded stream.");

        var storageKey = StorageKeys.NewSourceKey();
        await blobs.PutAsync(
            new BlobUpload(storageKey, request.Content, contentType, request.SizeBytes),
            cancellationToken);

        try
        {
            var voice = Voice.CreatePending(page.Id, VoiceTitle.Create(request.Title), storageKey, contentType, request.SizeBytes);
            await voices.AddAsync(voice, cancellationToken);
            await unitOfWork.SaveChanges(cancellationToken);
            return new UploadVoiceResponse(
                voice.Id.Value,
                voice.PageId.Value,
                voice.Title.Value,
                voice.Status.ToString(),
                voice.Audio.SizeBytes,
                voice.Audio.SourceContentType);
        }
        catch (Exception saveFailure)
        {
            try
            {
                await blobs.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch (Exception deleteFailure)
            {
                throw new AggregateException(
                    "The voice was not saved and the source object could not be deleted.",
                    saveFailure,
                    deleteFailure);
            }

            throw;
        }
    }
}
