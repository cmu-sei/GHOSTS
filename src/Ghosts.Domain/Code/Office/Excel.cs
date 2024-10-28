// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Ghosts.Domain.Code.Office
{
    public class Excel
    {
        public static void Write(string filePath, string content)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                var workbook = new XSSFWorkbook();

                var sheet1 = workbook.CreateSheet("Sheet1");

                sheet1.AddMergedRegion(new CellRangeAddress(0, 0, 0, 10));
                var rowIndex = 0;
                var row = sheet1.CreateRow(rowIndex);
                row.Height = 30 * 80;
                row.CreateCell(0).SetCellValue(content);
                sheet1.AutoSizeColumn(0);
                rowIndex++;

                var sheet2 = workbook.CreateSheet("Sheet2");
                var style1 = workbook.CreateCellStyle();
                style1.FillForegroundColor = HSSFColor.Blue.Index2;
                style1.FillPattern = FillPattern.SolidForeground;

                var style2 = workbook.CreateCellStyle();
                style2.FillForegroundColor = HSSFColor.Yellow.Index2;
                style2.FillPattern = FillPattern.SolidForeground;

                var cell2 = sheet2.CreateRow(0).CreateCell(0);
                cell2.CellStyle = style1;
                cell2.SetCellValue(0);

                cell2 = sheet2.CreateRow(1).CreateCell(0);
                cell2.CellStyle = style2;
                cell2.SetCellValue(1);

                workbook.Write(fs);
            }
        }
    }
}
