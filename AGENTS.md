# Repository Guidelines

LinuxGameCompat is a .NET 10 Blazor Server application that presents source-backed Linux game compatibility evidence. Extended information is in @README.md

## Working Rules and Core Commands

- This is Minimum Viable Product (MVP). Avoid over-engineering (unnecessary complexity, code that is likely to be speculative abstractions, dead flexibility etc.) when designing features.
- Never commit credentials or local environment files. `.env*` is ignored, and summary generation reads `OPENAI_API_KEY` only at runtime.
- Apply schema changes after code passes all verification with `dotnet ef database update --project LinuxGameCompat/LinuxGameCompat.csproj`; review generated migrations and the model snapshot together.

## Coding and Testing Conventions

- Use explicit local variable types except when the type is already visible in the expression, such as new TypeName(...).
- When a service feature requires three or more related implementation files, place them in a feature-named subfolder under `LinuxGameCompat/Services/`.
