# MultiTagInvertedIndex

Fast multi-tag inverted index for .NET with bitmap-based querying and composable boolean rules.

## Features

* Fast bitmap-based tag queries
* Composable boolean rules
* Optional lazy result enumeration
* Immutable query rule objects

## Use Cases

MultiTagInvertedIndex is useful for:

* File indexing systems
* Asset databases
* UI filtering systems
* Search panels
* Tag-based content systems

## Installation

Download the latest release archive from the
[GitHub Releases](../../releases) page.

The archive contains:

```text
netstandard2.0/
├── MultiTagInvertedIndex.dll
├── MultiTagInvertedIndex.xml
└── MultiTagInvertedIndex.pdb
```

### Files

| File                        | Description                                           |
| --------------------------- | ----------------------------------------------------- |
| `MultiTagInvertedIndex.dll` | Main library assembly                                 |
| `MultiTagInvertedIndex.xml` | IntelliSense XML documentation                        |
| `MultiTagInvertedIndex.pdb` | Debug symbols for debugging and readable stack traces |

### Using the Library

Reference the DLL in your project.

Place the XML and PDB files next to the assembly to enable:

* IntelliSense documentation
* Source navigation
* Better debugging support
* Readable stack traces

### Example `.csproj` Reference

```xml
<ItemGroup>
    <Reference Include="MultiTagInvertedIndex">
        <HintPath>libs/netstandard2.0/MultiTagInvertedIndex.dll</HintPath>
    </Reference>
</ItemGroup>
```

## Quick Start

```csharp
using Indexing;
using MultiTagInvertedIndex.Rules;

MultiTagIndex<string, string> index = new();

index["cat.png"] = ["image", "png"];
index["dog.jpg"] = ["image", "jpg"];

var images = index.GetValues("image");
// [ "cat.png", "dog.jpg" ]

var pngImages = index.GetValues(
    Rule.Tag("image") & Rule.Tag("png")
);
// [ "cat.png" ]
```

## Example Queries

```csharp
Rule.Tag("image") & "public"
Rule.Tag("audio") | "video"
~Rule.Tag("private")
Rule.Tag("png") ^ Rule.Tag("gif")
```

## Rule Operators

| Operator | Meaning |
| -------- | ------- |
| `&`      | AND     |
| `\|`     | OR      |
| `^`      | XOR     |
| `~`      | NOT     |

## Implicit Conversion Notes

Implicit conversion from `TTag` to `Rule<TTag>` works for simple expressions:

```csharp
Rule.Tag("image") & "png"
```

However, complex nested expressions may require explicit `Rule.Tag(...)`
calls because of C# operator resolution rules.

## Lazy Enumeration

`GetValues()` materializes the result into memory.

`EnumerateValues()` yields values lazily and can be useful for:

* Large datasets
* Incremental UI loading
* Streaming-like processing
* Memory-sensitive scenarios

Example:

```csharp
foreach (string file in index.EnumerateValues(Rule.Tag("image")))
{
    Console.WriteLine(file);
}
```

## Core API

| Method              | Description                             |
| ------------------- | --------------------------------------- |
| `AddTags()`         | Associates tags with a value            |
| `RemoveTags()`      | Removes tag associations                |
| `GetValues()`       | Returns matching values                 |
| `EnumerateValues()` | Lazily enumerates matching values       |
| `Contains()`        | Checks whether any value matches a rule |
| `Count()`           | Counts matching values                  |

## Performance

The library uses roaring bitmaps internally for efficient set operations.

Query performance depends mostly on:

* Bitmap density
* Rule complexity
* Number of matching values
* Amount of bitmap intersections

## Thread Safety

`MultiTagIndex` is not thread-safe.

Concurrent reads are safe only while the collection is not being modified.

## Dependencies

This library uses:

* [RoaringBitmap](https://github.com/Tornhoof/RoaringBitmap)

## License

MIT

## Author

MAWE-lab

GitHub:
[https://github.com/MAWE-lab](https://github.com/MAWE-lab)
