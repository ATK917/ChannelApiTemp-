using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using ChannelApiTemp.Models;

namespace ChannelApiTemp.Helpers
{
    public static class FileParser
    {
        public static List<Channel> ParseCsv(Stream stream)
        {
            var channels = new List<Channel>();

            using (var reader = new StreamReader(stream))
            {
                int row = 0;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    row++;

                    // Başlık satırını atla
                    if (row == 1)
                        continue;

                    var values = line.Split(',');

                    if (values.Length < 4)
                        continue;

                    int subscribers = 0;
                    int.TryParse(values[2], out subscribers);

                    var channel = new Channel
                    {
                        Name = values[0],
                        Url = values[1],
                        Subscribers = subscribers,
                        Category = values[3]
                    };

                    channels.Add(channel);
                }
            }

            return channels;
        }

        public static List<Channel> ParseExcel(Stream stream)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets[0];
            int rowCount = worksheet.Dimension.Rows;

            var channels = new List<Channel>();

            for (int row = 2; row <= rowCount; row++) // 1. satır başlık olduğu için 2'den başla
            {
                if (string.IsNullOrWhiteSpace(worksheet.Cells[row, 1].Text))
                    continue;

                int subscribers = 0;
                int.TryParse(worksheet.Cells[row, 3].Text, out subscribers);

                var channel = new Channel
                {
                    Name = worksheet.Cells[row, 1].Text,
                    Url = worksheet.Cells[row, 2].Text,
                    Subscribers = subscribers,
                    Category = worksheet.Cells[row, 4].Text
                };

                channels.Add(channel);
            }

            return channels;
        }

        public static List<Channel> ParseCsvFolder(string folderPath)
        {
            var allChannels = new List<Channel>();
            var files = Directory.GetFiles(folderPath, "*.csv");

            foreach (var file in files)
            {
                using var fileStream = File.OpenRead(file);
                var channels = ParseCsv(fileStream);
                allChannels.AddRange(channels);
            }

            return allChannels;
        }

        public static List<Channel> ParseExcelFolder(string folderPath)
        {
            var allChannels = new List<Channel>();
            var files = Directory.GetFiles(folderPath, "*.xlsx");

            foreach (var file in files)
            {
                using var fileStream = File.OpenRead(file);
                var channels = ParseExcel(fileStream);
                allChannels.AddRange(channels);
            }

            return allChannels;
        }
    }
}
