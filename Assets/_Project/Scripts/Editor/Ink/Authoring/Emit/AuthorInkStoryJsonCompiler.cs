#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

internal static class AuthorInkStoryJsonCompiler
{
    internal static AuthorInkSharedContext AnalyzeSources(IEnumerable<string> sources)
    {
        return AuthorInkSourceAnalyzer.Analyze(sources);
    }

    internal static bool TryCompile(
        string source,
        AuthorInkCompileOptions options,
        AuthorInkSharedContext shared,
        out string json,
        out AuthorInkImportReport report,
        out string error)
    {
        json = "";
        error = "";
        report = new AuthorInkImportReport();

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "Author Ink source пуст.";
            return false;
        }

        if (options == null || string.IsNullOrWhiteSpace(options.EpisodeId))
        {
            error = "Не задан EpisodeId для Author Ink compiler.";
            return false;
        }

        try
        {
            var parser = new AuthorInkSourceParser(source, report);
            List<AuthorInkStatement> statements = parser.Parse();
            var emitter = new AuthorInkStoryJsonEmitter(options, shared ?? new AuthorInkSharedContext(), report);
            StoryJsonDocument document = emitter.Emit(statements);
            json = JsonUtility.ToJson(document, true);
            if (!StoryJsonConverter.IsCanonicalJson(json))
            {
                error = "Author Ink compiler создал неканонический Story JSON.";
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }
}
#endif
