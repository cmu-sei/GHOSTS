// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
using NPOI.XWPF.UserModel;

namespace Ghosts.Domain.Code.Office
{
    public class Word
    {
        public static void Write(string filePath, string title, string content)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                var doc = new XWPFDocument();
                var p0 = doc.CreateParagraph();
                p0.Alignment = ParagraphAlignment.CENTER;
                var r0 = p0.CreateRun();
                r0.FontFamily = "Tahoma";
                r0.FontSize = 18;
                r0.IsBold = true;
                r0.SetText(title);

                var p1 = doc.CreateParagraph();
                p1.Alignment = ParagraphAlignment.LEFT;
                p1.IndentationFirstLine = 500;
                var r1 = p1.CreateRun();
                r1.FontFamily = "Tahoma";
                r1.FontSize = 12;
                r1.IsBold = true;
                r1.SetText(content);

                doc.Write(fs);
            }
        }
    }
}
