# Coding Guidelines

## Backend (.NET 10 / C#)

### Naming

| Element | Convention | Example |
|---|---|---|
| Classes, Interfaces | PascalCase | `ApiaryService`, `IApiaryService` |
| Methods | PascalCase | `GetWithBeehivesAsync` |
| Parameters, locals | camelCase | `apiaryId`, `createDto` |
| Private fields | `_camelCase` | `_unitOfWork` |
| DTOs | Noun + suffix | `CreateApiaryDto`, `ApiaryDetailDto` |
| Validators | Entity + "Validator" | `CreateApiaryValidator` |
| Controllers | Plural noun | `ApiariesController` |
| Repos | `I` prefix + plural | `IApiaryRepository` |

### File & Folder Structure

**One public type per file.** Each feature folder holds its service interface + implementation,
its DTOs (one per file under `DTOs/`), its validators (one per file under `Validators/`), and its
AutoMapper profile.

```
Application/
  Apiaries/
    IApiaryService.cs
    ApiaryService.cs
    ApiaryMappingProfile.cs
    DTOs/
      ApiaryDto.cs
      ApiaryDetailDto.cs
      CreateApiaryDto.cs
      UpdateApiaryDto.cs
    Validators/
      CreateApiaryValidator.cs
      UpdateApiaryValidator.cs
```

### Async Rules

- All I/O methods must be `async Task<T>` — no `.Result` or `.Wait()`
- Use `CancellationToken` on public service methods when relevant
- Never `async void` — use `async Task`

### Error Handling

- Throw `NotFoundException` when an entity is not found by ID
- Throw `BusinessRuleException` for domain constraint violations
- Throw `ValidationException` for invalid input (FluentValidation handles this automatically)
- Never catch and swallow exceptions silently
- Never return `null` from services — throw instead

```csharp
// Correct
var apiary = await _unitOfWork.Apiaries.GetByIdAsync(id)
    ?? throw new NotFoundException(nameof(Apiary), id);

// Wrong
var apiary = await _unitOfWork.Apiaries.GetByIdAsync(id);
if (apiary == null) return null;
```

### Validators (FluentValidation)

```csharp
public class CreateApiaryValidator : AbstractValidator<CreateApiaryDto>
{
    public CreateApiaryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
    }
}
```

- Validators are registered via `AddValidatorsFromAssemblyContaining<…>()` and invoked explicitly in controllers (`await validator.ValidateAsync(dto)`). Auto-validation is intentionally not enabled, to keep the error-response shape stable for the frontend.
- All create/update DTOs must have a corresponding validator, co-located under the feature's `Validators/` folder (one validator per file)

### AutoMapper

- All mappings live in `MappingProfile.cs`
- Never use `_mapper.Map<T>()` outside the Application layer
- Add reverse maps only when actually needed

### Entity Framework

- Query with `Include()` only in repository methods, never in services
- Use `AsNoTracking()` in read-only queries
- Never call `_context.SaveChangesAsync()` directly — always use `_unitOfWork.SaveChangesAsync()`

---

## Frontend (React / TypeScript)

### Naming

| Element | Convention | Example |
|---|---|---|
| Components, Pages | PascalCase | `ApiaryDetailPage`, `TodoSection` |
| Hooks | `use` prefix | `useApiaries`, `useCreateApiary` |
| Service functions | camelCase verb | `getApiaryById`, `createBeehive` |
| TS interfaces | PascalCase | `ApiaryDetail`, `CreateApiaryPayload` |
| Enums | PascalCase | `BeehiveType`, `DietStatus` |
| Files | PascalCase for components | `ApiaryListPage.tsx` |
| Files | camelCase for services/utils | `apiaryService.ts`, `queries.ts` |

### Component Rules

- Pages go in `features/<domain>/`
- Reusable UI goes in `shared/components/`
- No business logic in components — extract to hooks or services
- Keep components focused: if a component exceeds ~150 lines, split it

### Data Fetching

- Always use React Query hooks from `queries.ts` — never call service functions directly in components
- Mutations must invalidate related query keys on success
- Handle `isLoading`, `isError` states in every page component

```tsx
// Correct
const { data: apiary, isLoading } = useApiaryDetail(id);

// Wrong
const [apiary, setApiary] = useState(null);
useEffect(() => { apiaryService.getById(id).then(setApiary); }, [id]);
```

### Forms

- All forms use React Hook Form — no controlled `useState` per field
- Submit handler calls a React Query mutation
- Validate on submit; show field-level errors inline

### TypeScript

- `strict: true` — no `any`, no `as any`
- All API response shapes typed with interfaces from `core/models/index.ts`
- Enums defined in models mirror backend enums exactly

### Styling

- Tailwind utility classes only — no inline `style={{}}` unless strictly required
- Color tokens follow the honey/amber theme (`amber-*`, `yellow-*`)
- Responsive breakpoints: mobile-first (`sm:`, `md:`, `lg:`)

### Error & Loading States

```tsx
if (isLoading) return <div>Loading...</div>;
if (isError) return <div>Error loading data.</div>;
```

Every page that fetches data must handle both states before rendering content.
