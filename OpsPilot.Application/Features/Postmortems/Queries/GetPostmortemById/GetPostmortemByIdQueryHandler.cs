using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Queries.GetPostmortemById;

public sealed class GetPostmortemByIdQueryHandler
    : IRequestHandler<GetPostmortemByIdQuery, PostmortemDto?>
{
    private readonly IPostmortemRepository _repository;

    public GetPostmortemByIdQueryHandler(IPostmortemRepository repository) =>
        _repository = repository;

    public async Task<PostmortemDto?> Handle(
        GetPostmortemByIdQuery request,
        CancellationToken cancellationToken)
    {
        var postmortem = await _repository.GetByIdAsync(request.Id, cancellationToken);

        return postmortem?.ToDto();
    }
}
