using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine.UIElements;
using MdRenderer = UIMarkdownRenderer.UIMarkdownRenderer;

namespace Rtl.MarkdownRenderer.Tests.EditMode
{
    /// <summary>Общие помощники EditMode-тестов рендера: создание рендерера и обход дерева.</summary>
    internal static class MarkdownTestUtility
    {
        public static MdRenderer CreateRenderer(bool includeDefaultStylesheet = false)
        {
            return new MdRenderer(
                (_, _) => { },
                false,
                includeDefaultStylesheet: includeDefaultStylesheet);
        }

        public static List<VisualElement> ElementsWithClass(VisualElement root, string className)
        {
            List<VisualElement> elements = new();
            root.Query<VisualElement>(className: className).ForEach(elements.Add);
            return elements;
        }

        public static VisualElement SingleElementWithClass(VisualElement root, string className)
        {
            List<VisualElement> elements = ElementsWithClass(root, className);
            Assert.That(elements, Has.Count.EqualTo(1), $"Ожидался ровно один элемент с классом {className}.");
            return elements[0];
        }

        public static List<Label> Labels(VisualElement root)
        {
            List<Label> labels = new();
            root.Query<Label>().ForEach(labels.Add);
            return labels;
        }

        public static string JoinedLabelText(VisualElement root) =>
            string.Join("\n", Labels(root).Select(label => label.text));

        /// <summary>Текст без тегов rich text и без нулевых пробелов экранирования — то, что видит читатель.</summary>
        public static string PlainText(string richText) =>
            Regex.Replace(richText ?? string.Empty, "</?(?:b|i|u|color|link)(?:=[^>]*)?>", string.Empty)
                .Replace("\u200B", string.Empty);

        public static string PlainLabelText(VisualElement root) => PlainText(JoinedLabelText(root));
    }
}
