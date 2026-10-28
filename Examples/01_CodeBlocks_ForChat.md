# Code Blocks For Chat

This file checks fenced code blocks that are important for AI chat, passwords, questions, and test payloads.

## Password block

```password
room: lab-a
password: 7391
hint: four digits, no spaces
```

## Quiz block

```quiz
question: Which keyword creates a class in C#?
answers:
  - class
  - object
  - prefab
correct: class
```

## True/false block

```truefalse
statement: WebGL builds must not reference UnityEditor APIs.
answer: true
```

## C# highlighting smoke

```csharp
public sealed class DoorCode
{
    public bool IsValid(string input)
    {
        return input == "7391";
    }
}
```

## Plain fenced block

```
No language info string here.
Symbols should remain readable: <tag> & value == true
```
