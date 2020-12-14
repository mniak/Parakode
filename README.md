![Build and Publish to nuget](https://github.com/mniak/Parakode/workflows/Build%20and%20Publish%20to%20nuget/badge.svg)

Parakode - C# Source Generators
==============

## Icon/Logo
![Cat](parakode-cat.png)

In the absence of a worthy logo, this kitten's picture was chosen.

## Documentation on C# Source Generators

- https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/
- https://devblogs.microsoft.com/dotnet/new-c-source-generator-samples/
- https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.md
- https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.cookbook.md
- http://letmegooglethat.com/?q=c%23+source+generators


## Source Generators

For now there is only a small number of generators in this repository.
Exactly one.

### Enum Description
Adds an extension method `GetDescription()` for each enum whose members contains the attribute `DescriptionAttribute`.

```
Install-Package Parakode.EnumDescription
```
