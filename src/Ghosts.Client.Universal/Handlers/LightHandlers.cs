// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Universal.Handlers;

public class LightHandlers
{
    internal static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private static string GetSavePath(Type cls, TimelineHandler handler, TimelineEvent timelineEvent,
        string fileExtension, CancellationToken token)
    {
        _log.Trace($"{cls} event - {timelineEvent}");
        WorkingHours.Is(handler);

        if (timelineEvent.DelayBeforeActual > 0)
        {
            if (token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual)) token.ThrowIfCancellationRequested();
        }

        if (token.WaitHandle.WaitOne(3000)) token.ThrowIfCancellationRequested();

        var rand = RandomFilename.Generate();

        var dir = timelineEvent.CommandArgs[0].ToString();
        if (dir.Contains("%"))
        {
            dir = Environment.ExpandEnvironmentVariables(dir);
        }

        if (Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var path = $"{dir}\\{rand}.{fileExtension}";

        //if directory does not exist, create!
        _log.Trace($"Checking directory at {path}");
        var f = new FileInfo(path).Directory;
        if (f == null)
        {
            _log.Trace($"Directory does not exist, creating directory at {path}");
            Directory.CreateDirectory(path);
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Debug(e);
        }

        _log.Trace($"{cls} saving to path - {path}");
        return path;
    }

    public class LightWordHandler(
        Timeline entireTimeline,
        TimelineHandler timelineHandler,
        CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        protected override Task RunOnce()
        {
            try
            {
                foreach (var timelineEvent in this.Handler.TimeLineEvents)
                {
                    var path = GetSavePath(typeof(LightExcelHandler), this.Handler, timelineEvent, "docx", cancellationToken);

                    var list = RandomText.GetDictionary.GetDictionaryList();
                    using (var rt = new RandomText(list))
                    {
                        rt.AddSentence(5);

                        var title = rt.Content;
                        rt.AddContentParagraphs(2, 3, 5, 7, 22);
                        var paragraph = rt.Content;
                        Domain.Code.Office.Word.Write(path, title, paragraph);
                    }

                    FileListing.Add(path, this.Handler.HandlerType);
                    Report(new ReportItem
                    {
                        Handler = this.Handler.HandlerType.ToString(),
                        Command = timelineEvent.Command,
                        Arg = timelineEvent.CommandArgs[0].ToString(),
                        Trackable = timelineEvent.TrackableId
                    });
                }
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                {
                    throw;
                }
                _log.Error(e);
            }

            return Task.CompletedTask;
        }
    }

    public class LightPowerPointHandler(
        Timeline entireTimeline,
        TimelineHandler timelineHandler,
        CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        protected override Task RunOnce()
        {
            try
            {
                foreach (var timelineEvent in this.Handler.TimeLineEvents)
                {
                    var path = GetSavePath(typeof(LightPowerPointHandler), this.Handler, timelineEvent, "pptx", cancellationToken);

                    var list = RandomText.GetDictionary.GetDictionaryList();
                    using (var rt = new RandomText(list))
                    {
                        rt.AddSentence(5);
                        var title = rt.Content;
                        rt.AddContentParagraphs(1, 2, 3, 5, 15);
                        var content = rt.Content;
                        CreatePowerPoint(path, title, content);
                    }

                    FileListing.Add(path, this.Handler.HandlerType);
                    Report(new ReportItem
                    {
                        Handler = this.Handler.HandlerType.ToString(),
                        Command = timelineEvent.Command,
                        Arg = timelineEvent.CommandArgs[0].ToString(),
                        Trackable = timelineEvent.TrackableId
                    });
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            return Task.CompletedTask;
        }

        private static void CreatePowerPoint(string filePath, string title, string content)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            // [Content_Types].xml
            AddEntry(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>" +
                "<Override PartName=\"/ppt/slides/slide1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>" +
                "<Override PartName=\"/ppt/slideLayouts/slideLayout1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/>" +
                "<Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/>" +
                "</Types>");

            // _rels/.rels
            AddEntry(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"ppt/presentation.xml\"/>" +
                "</Relationships>");

            // ppt/presentation.xml
            AddEntry(archive, "ppt/presentation.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<p:presentation xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">" +
                "<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>" +
                "<p:sldIdLst><p:sldId id=\"256\" r:id=\"rId2\"/></p:sldIdLst>" +
                "<p:sldSz cx=\"9144000\" cy=\"6858000\" type=\"screen4x3\"/>" +
                "<p:notesSz cx=\"6858000\" cy=\"9144000\"/>" +
                "</p:presentation>");

            // ppt/_rels/presentation.xml.rels
            AddEntry(archive, "ppt/_rels/presentation.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"slideMasters/slideMaster1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide1.xml\"/>" +
                "</Relationships>");

            // ppt/slides/slide1.xml
            AddEntry(archive, "ppt/slides/slide1.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<p:sld xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">" +
                "<p:cSld><p:spTree>" +
                "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
                "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>" +
                "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Title 1\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"title\"/></p:nvPr></p:nvSpPr>" +
                "<p:spPr/>" +
                "<p:txBody><a:bodyProperties/><a:lstStyle/><a:p><a:r><a:rPr lang=\"en-US\" dirty=\"0\"/><a:t>" + EscapeXml(title) + "</a:t></a:r></a:p></p:txBody></p:sp>" +
                "<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Content 2\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph idx=\"1\"/></p:nvPr></p:nvSpPr>" +
                "<p:spPr/>" +
                "<p:txBody><a:bodyProperties/><a:lstStyle/><a:p><a:r><a:rPr lang=\"en-US\" dirty=\"0\"/><a:t>" + EscapeXml(content) + "</a:t></a:r></a:p></p:txBody></p:sp>" +
                "</p:spTree></p:cSld>" +
                "<p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>" +
                "</p:sld>");

            // ppt/slides/_rels/slide1.xml.rels
            AddEntry(archive, "ppt/slides/_rels/slide1.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/>" +
                "</Relationships>");

            // ppt/slideLayouts/slideLayout1.xml
            AddEntry(archive, "ppt/slideLayouts/slideLayout1.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<p:sldLayout xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" type=\"obj\" preserve=\"1\">" +
                "<p:cSld name=\"Title and Content\"><p:spTree>" +
                "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
                "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>" +
                "</p:spTree></p:cSld>" +
                "<p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>" +
                "</p:sldLayout>");

            // ppt/slideLayouts/_rels/slideLayout1.xml.rels
            AddEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"../slideMasters/slideMaster1.xml\"/>" +
                "</Relationships>");

            // ppt/slideMasters/slideMaster1.xml
            AddEntry(archive, "ppt/slideMasters/slideMaster1.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<p:sldMaster xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\">" +
                "<p:cSld><p:spTree>" +
                "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
                "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>" +
                "</p:spTree></p:cSld>" +
                "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
                "<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/></p:sldLayoutIdLst>" +
                "</p:sldMaster>");

            // ppt/slideMasters/_rels/slideMaster1.xml.rels
            AddEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/>" +
                "</Relationships>");
        }

        private static void AddEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        private static string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }

    public class LightExcelHandler(
        Timeline entireTimeline,
        TimelineHandler timelineHandler,
        CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        protected override Task RunOnce()
        {
            try
            {
                foreach (var timelineEvent in this.Handler.TimeLineEvents)
                {
                    var path = GetSavePath(typeof(LightExcelHandler), this.Handler, timelineEvent, "xlsx", cancellationToken);

                    var list = RandomText.GetDictionary.GetDictionaryList();
                    using (var rt = new RandomText(list))
                    {
                        rt.AddSentence(5);
                        Domain.Code.Office.Excel.Write(path, rt.Content);
                    }

                    FileListing.Add(path, this.Handler.HandlerType);
                    Report(new ReportItem
                    {
                        Handler = this.Handler.HandlerType.ToString(),
                        Command = timelineEvent.Command,
                        Arg = timelineEvent.CommandArgs[0].ToString(),
                        Trackable = timelineEvent.TrackableId
                    });
                }
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                {
                    throw;
                }
                _log.Error(e);
            }

            return Task.CompletedTask;
        }
    }
}
