using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Text;
using OfficeOpenXml;

using ChannelApiTemp.Models;
using ChannelApiTemp.Helpers;

namespace ChannelApiTemp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChannelController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ChannelController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --------------------------------------------------------------------
        // 1) Tek dosya yükleme (CSV veya Excel)
        // POST: /api/Channel/upload-file
        // --------------------------------------------------------------------
        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Dosya yüklenemedi.");

            List<Channel> channels;

            using (var stream = file.OpenReadStream())
            {
                if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    channels = FileParser.ParseCsv(stream);
                }
                else if (file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    channels = FileParser.ParseExcel(stream);
                }
                else
                {
                    return BadRequest("Desteklenmeyen dosya formatı. Sadece .csv ve .xlsx desteklenir.");
                }
            }

            if (channels.Count == 0)
                return BadRequest("Dosyada kayıt bulunamadı.");

            _context.Channels.AddRange(channels);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"{channels.Count} kanal başarıyla eklendi." });
        }

        // --------------------------------------------------------------------
        // 2) Klasörden toplu yükleme (CSV ve Excel)
        // POST: /api/Channel/upload-folder?folderPath=C:\Data\Channels
        // --------------------------------------------------------------------
        [HttpPost("upload-folder")]
        public async Task<IActionResult> UploadFolder([FromQuery] string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return BadRequest("Geçerli bir klasör yolu giriniz.");

            var allChannels = new List<Channel>();

            // Helper içindeki klasör okuma fonksiyonları
            var csvChannels = FileParser.ParseCsvFolder(folderPath);
            var excelChannels = FileParser.ParseExcelFolder(folderPath);

            allChannels.AddRange(csvChannels);
            allChannels.AddRange(excelChannels);

            if (!allChannels.Any())
                return BadRequest("Klasörde geçerli .csv/.xlsx dosyası bulunamadı.");

            _context.Channels.AddRange(allChannels);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"{allChannels.Count} kanal başarıyla eklendi." });
        }

        // --------------------------------------------------------------------
        // 3) Basit tüm liste (ham)
        // GET: /api/Channel
        // --------------------------------------------------------------------
        [HttpGet]
        public IActionResult GetAll()
        {
            var channels = _context.Channels.AsNoTracking().ToList();
            return Ok(channels);
        }

        // --------------------------------------------------------------------
        // 4) Zengin listeleme (arama/filtre/sıralama + sayfalama)
        // GET: /api/Channel/list?search=ruhi&category=Eğitim&minSubs=1000000&sort=subscribers_desc&page=1&pageSize=20
        // sort: name_asc | name_desc | subscribers_asc | subscribers_desc | category_asc | category_desc
        // --------------------------------------------------------------------
        [HttpGet("list")]
        public IActionResult List(
            [FromQuery] string? search,
            [FromQuery] string? category,
            [FromQuery] int? minSubs,
            [FromQuery] int? maxSubs,
            [FromQuery] string? sort = "name_asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 20;

            var query = _context.Channels.AsNoTracking().AsQueryable();

            // Arama: ad veya URL içinde
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.Name.ToLowerInvariant().Contains(s) ||
                    x.Url.ToLowerInvariant().Contains(s));
            }

            // Kategori filtresi
            if (!string.IsNullOrWhiteSpace(category))
            {
                var c = category.Trim().ToLowerInvariant();
                query = query.Where(x => x.Category.ToLowerInvariant() == c);
            }

            // Abone sayısı aralığı
            if (minSubs.HasValue)
                query = query.Where(x => x.Subscribers >= minSubs.Value);

            if (maxSubs.HasValue)
                query = query.Where(x => x.Subscribers <= maxSubs.Value);

            // Sıralama
            query = (sort ?? "name_asc").ToLowerInvariant() switch
            {
                "name_desc"         => query.OrderByDescending(x => x.Name),
                "subscribers_asc"   => query.OrderBy(x => x.Subscribers),
                "subscribers_desc"  => query.OrderByDescending(x => x.Subscribers),
                "category_asc"      => query.OrderBy(x => x.Category).ThenBy(x => x.Name),
                "category_desc"     => query.OrderByDescending(x => x.Category).ThenBy(x => x.Name),
                _                   => query.OrderBy(x => x.Name), // name_asc (default)
            };

            // Sayfalama
            var totalCount = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                page,
                pageSize,
                totalCount,
                items
            });
        }

        // --------------------------------------------------------------------
        // 5) Export (CSV/XLSX)
        // GET: /api/Channel/export?format=csv  (veya xlsx)
        // --------------------------------------------------------------------
        [HttpGet("export")]
        public IActionResult Export([FromQuery] string format = "csv")
        {
            var data = _context.Channels
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToList();

            if (!data.Any())
                return BadRequest("Dışa aktarılacak kayıt bulunamadı.");

            format = (format ?? "csv").Trim().ToLowerInvariant();

            return format switch
            {
                "xlsx" => ExportAsExcel(data),
                _      => ExportAsCsv(data)
            };
        }

        private IActionResult ExportAsCsv(List<Channel> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Name,Url,Subscribers,Category");

            string Esc(string? s)
            {
                s ??= "";
                if (s.Contains('"') || s.Contains(','))
                    return $"\"{s.Replace("\"", "\"\"")}\"";
                return s;
            }

            foreach (var r in rows)
            {
                sb.AppendLine($"{r.Id},{Esc(r.Name)},{Esc(r.Url)},{r.Subscribers},{Esc(r.Category)}");
            }

            var payload = Encoding.UTF8.GetBytes(sb.ToString());
            // Excel’in Türkçe karakterleri doğru göstermesi için BOM ekleyelim
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(payload).ToArray();

            return File(bytes, "text/csv", $"channels_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
        }

        private IActionResult ExportAsExcel(List<Channel> rows)
        {
            // EPPlus lisans bağlamı
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Channels");

            // Header
            ws.Cells[1, 1].Value = "Id";
            ws.Cells[1, 2].Value = "Name";
            ws.Cells[1, 3].Value = "Url";
            ws.Cells[1, 4].Value = "Subscribers";
            ws.Cells[1, 5].Value = "Category";

            // Data
            int r = 2;
            foreach (var x in rows)
            {
                ws.Cells[r, 1].Value = x.Id;
                ws.Cells[r, 2].Value = x.Name;
                ws.Cells[r, 3].Value = x.Url;
                ws.Cells[r, 4].Value = x.Subscribers;
                ws.Cells[r, 5].Value = x.Category;
                r++;
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            var bytes = package.GetAsByteArray();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"channels_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
        }
    }
}
