# Common Patterns

Reference these templates when adding new features. Deviate only with justification.

---

## Backend: Adding a New Feature

### 1. Domain Entity

```csharp
// Melarium.Domain/Entities/MyEntity.cs
public class MyEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
}
```

### 2. Repository Interface + Implementation

```csharp
// Melarium.Domain/Interfaces/IMyEntityRepository.cs
public interface IMyEntityRepository : IRepository<MyEntity>
{
    Task<IEnumerable<MyEntity>> GetByOrganizationAsync(Guid orgId);
}

// Melarium.Infrastructure/Repositories/MyEntityRepository.cs
public class MyEntityRepository : Repository<MyEntity>, IMyEntityRepository
{
    public MyEntityRepository(MelariumDbContext context) : base(context) { }

    public async Task<IEnumerable<MyEntity>> GetByOrganizationAsync(Guid orgId) =>
        await _context.MyEntities
            .Where(e => e.OrganizationId == orgId)
            .AsNoTracking()
            .ToListAsync();
}
```

### 3. Unit of Work — Add Property

```csharp
// In IUnitOfWork and UnitOfWork:
IMyEntityRepository MyEntities { get; }
```

### 4. DTOs + Validator

```csharp
// Melarium.Application/MyEntities/MyEntityDto.cs
public record MyEntityDto(Guid Id, string Name, Guid OrganizationId, DateTime CreatedAt);
public record CreateMyEntityDto(string Name, Guid OrganizationId);
public record UpdateMyEntityDto(string Name);

public class CreateMyEntityValidator : AbstractValidator<CreateMyEntityDto>
{
    public CreateMyEntityValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OrganizationId).NotEmpty();
    }
}
```

### 5. Service

```csharp
public class MyEntityService : IMyEntityService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public MyEntityService(IUnitOfWork uow, IMapper mapper)
        => (_uow, _mapper) = (uow, mapper);

    public async Task<IEnumerable<MyEntityDto>> GetAllAsync(Guid orgId)
    {
        var entities = await _uow.MyEntities.GetByOrganizationAsync(orgId);
        return _mapper.Map<IEnumerable<MyEntityDto>>(entities);
    }

    public async Task<MyEntityDto> GetByIdAsync(Guid id)
    {
        var entity = await _uow.MyEntities.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(MyEntity), id);
        return _mapper.Map<MyEntityDto>(entity);
    }

    public async Task<MyEntityDto> CreateAsync(CreateMyEntityDto dto)
    {
        var entity = _mapper.Map<MyEntity>(dto);
        await _uow.MyEntities.AddAsync(entity);
        await _uow.SaveChangesAsync();
        return _mapper.Map<MyEntityDto>(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _uow.MyEntities.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(MyEntity), id);
        await _uow.MyEntities.DeleteAsync(entity);
        await _uow.SaveChangesAsync();
    }
}
```

### 6. Controller

```csharp
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MyEntitiesController : ControllerBase
{
    private readonly IMyEntityService _service;
    public MyEntitiesController(IMyEntityService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MyEntityDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var orgId = Guid.Parse(User.FindFirst("organizationId")!.Value);
        return Ok(await _service.GetAllAsync(orgId));
    }

    [HttpPost]
    [ProducesResponseType(typeof(MyEntityDto), 201)]
    public async Task<IActionResult> Create([FromBody] CreateMyEntityDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
```

### 7. AutoMapper — Add to MappingProfile

```csharp
CreateMap<MyEntity, MyEntityDto>();
CreateMap<CreateMyEntityDto, MyEntity>();
CreateMap<UpdateMyEntityDto, MyEntity>();
```

### 8. Register in Program.cs

```csharp
builder.Services.AddScoped<IMyEntityService, MyEntityService>();
// Repository registered via UnitOfWork — no extra registration needed
```

---

## Frontend: Adding a New Feature

### 1. Model (core/models/index.ts)

```ts
export interface MyEntity {
  id: string;
  name: string;
  organizationId: string;
  createdAt: string;
}

export interface CreateMyEntityPayload {
  name: string;
  organizationId: string;
}
```

### 2. Service (core/services/myEntityService.ts)

```ts
import apiClient from './apiClient';
import { MyEntity, CreateMyEntityPayload } from '../models';

const BASE = '/my-entities';

export const myEntityService = {
  getAll: () => apiClient.get<MyEntity[]>(BASE).then(r => r.data),
  getById: (id: string) => apiClient.get<MyEntity>(`${BASE}/${id}`).then(r => r.data),
  create: (payload: CreateMyEntityPayload) => apiClient.post<MyEntity>(BASE, payload).then(r => r.data),
  update: (id: string, payload: Partial<CreateMyEntityPayload>) =>
    apiClient.put<MyEntity>(`${BASE}/${id}`, payload).then(r => r.data),
  delete: (id: string) => apiClient.delete(`${BASE}/${id}`),
};
```

### 3. React Query Hooks (core/services/queries.ts)

```ts
export const useMyEntities = () =>
  useQuery({ queryKey: ['myEntities'], queryFn: myEntityService.getAll });

export const useMyEntity = (id: string) =>
  useQuery({ queryKey: ['myEntities', id], queryFn: () => myEntityService.getById(id) });

export const useCreateMyEntity = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: myEntityService.create,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['myEntities'] }),
  });
};

export const useDeleteMyEntity = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: myEntityService.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['myEntities'] }),
  });
};
```

### 4. Page Component (features/myEntities/MyEntityListPage.tsx)

```tsx
export default function MyEntityListPage() {
  const { data, isLoading, isError } = useMyEntities();

  if (isLoading) return <div>Loading...</div>;
  if (isError) return <div>Error loading data.</div>;

  return (
    <div>
      {data?.map(e => <div key={e.id}>{e.name}</div>)}
    </div>
  );
}
```

### 5. Route (App.tsx)

```tsx
<Route path="/my-entities" element={<MyEntityListPage />} />
<Route path="/my-entities/:id" element={<MyEntityDetailPage />} />
```

---

## Extract JWT Claim in Controller

```csharp
var userId = Guid.Parse(User.FindFirst("userId")!.Value);
var orgId  = Guid.Parse(User.FindFirst("organizationId")!.Value);
var role   = User.FindFirst(ClaimTypes.Role)!.Value;
```
