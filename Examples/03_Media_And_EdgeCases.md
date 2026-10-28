# Media And Edge Cases

This file checks edge cases that often break markdown UI.

## Long paragraph wrapping

This is a deliberately long paragraph. It should wrap inside the bubble or editor viewer without escaping outside its parent visual element. Long inline code should also wrap safely: `very-long-inline-code-token-with-many-segments-and-values-1234567890`.

## HTML entities inside code

```text
Expected decoded symbols in code blocks:
&lt;button&gt;Start&lt;/button&gt;
Tom &amp; Jerry
5 &gt; 3
```

## Tables as plain markdown fallback

| Key | Value |
| --- | --- |
| Renderer | UIMarkdownRenderer |
| Target | Editor + Runtime + WebGL |

If tables are not styled as real tables, they should still remain readable as text.

## Image syntax

The renderer should create an image placeholder/request for this URL. If the network blocks it, the rest of the document must still render.

![Remote placeholder image](https://placehold.co/320x120/png?text=Markdown+Image)

## Nested list

- Lesson
  - Question
    - Answer
- Result
  - Passed
  - Failed
