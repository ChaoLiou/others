using Dapper;
using ICSharpCode.SharpZipLib.Tar;
using Ionic.Zip;
using Ionic.Zlib;
using Ltc.WOData;
using Ltc.WOData.WODataSources;
using NLog;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace App.WOXmlToRawDataDB
{
    public class WORawData
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();
        public List<string> SourcePathList { get; set; }
        public SqlConnection Conn { get; set; } = new SqlConnection(ConnectionStringsAndAppSettings._PatentEP_CS);
        private int _count = 0;
        private WODataSource _woDataSource;

        public WORawData(List<string> newSourcePathList)
        {
            SourcePathList = newSourcePathList;
            _woDataSource = WODataSource.WipoFtp;
            _log.Debug($"Xml Source Path: {ConnectionStringsAndAppSettings.Biblio_ZipsPath}");
            _log.Debug($"Xml Source File Type: .tar.gz");
        }

        public WORawData(WODataSource woDataSource)
        {
            switch (woDataSource)
            {
                case WODataSource.PctBackfileBibliographic:
                    SourcePathList = Directory.EnumerateFiles(ConnectionStringsAndAppSettings.PctBackfileBibliographic_ZipsPath, "*.zip", SearchOption.TopDirectoryOnly).ToList();
                    _woDataSource = woDataSource;
                    _log.Debug($"Xml Source Path: {ConnectionStringsAndAppSettings.PctBackfileBibliographic_ZipsPath}");
                    _log.Debug($"Xml Source File Type: .zip");
                    break;

                case WODataSource.WipoFtp:
                    SourcePathList = Directory.EnumerateFiles(ConnectionStringsAndAppSettings.Biblio_ZipsPath, "*_xml.tar.gz", SearchOption.AllDirectories).ToList();
                    _woDataSource = woDataSource;
                    _log.Debug($"Xml Source Path: {ConnectionStringsAndAppSettings.Biblio_ZipsPath}");
                    _log.Debug($"Xml Source File Type: .tar.gz");
                    break;
            }
        }

        public void FullStepsProcess()
        {
            switch (_woDataSource)
            {
                case WODataSource.PctBackfileBibliographic:
                    foreach (var sourcePath in SourcePathList)
                    {
                        SingleZipProcess(sourcePath);
                    }
                    break;

                case WODataSource.WipoFtp:
                    foreach (var sourcePath in SourcePathList)
                    {
                        SingleTarGzProcess(sourcePath);
                    }
                    break;
            }
        }

        public void SingleTarGzProcess(string sourcePath)
        {
            Conn.Open();
            try
            {
                using (FileStream reader = File.OpenRead(sourcePath))
                {
                    using (GZipStream gzData = new GZipStream(reader, CompressionMode.Decompress, true))
                    {
                        TarInputStream tarIn = new TarInputStream(gzData);
                        TarEntry tarEntry;
                        while ((tarEntry = tarIn.GetNextEntry()) != null)
                        {
                            try
                            {
                                if (tarEntry.Name.ToUpper().EndsWith(".XML"))
                                {
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        tarIn.CopyEntryContents(ms);
                                        ms.Position = 0;
                                        using (var sr = new StreamReader(ms))
                                        {
                                            _log.Trace($"{tarEntry.Name.Substring(0, tarEntry.Name.LastIndexOf("/"))}");
                                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                                            var el = XElement.Parse(sr.ReadToEnd());
                                            var newWO = new NewWO(_woDataSource);
                                            if (newWO.Parse(el))
                                            {
                                                insert(newWO, sourcePath, tarEntry.Name);
                                                _count++;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                _log.Error(sourcePath);
                                _log.Error(tarEntry.Name);
                                _log.Error(e.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(sourcePath);
                _log.Error(e.Message);
            }

            Conn.Close();

            #region checking counts

            var volFolderPath = sourcePath.Substring(0, sourcePath.LastIndexOf("\\"));
            var expectedCount = NewWO.GetIndexlstCount(_woDataSource, Path.Combine(volFolderPath, ConnectionStringsAndAppSettings.IndexlstName));
            var sign = expectedCount == _count ? "==" : "<>";
            _log.Debug($"{Path.GetFileName(volFolderPath)}|Expected Count: {expectedCount} {sign} Real Count: {_count}");

            #endregion checking counts
        }

        private void SingleZipProcess(string sourcePath)
        {
            try
            {
                using (var zipFile = ZipFile.Read(sourcePath))
                {
                    foreach (var entry in zipFile.Where(x => x.FileName.ToUpper().EndsWith(".XML")))
                    {
                        try
                        {
                            _log.Trace($"{entry.FileName}");
                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                            using (var sr = new StreamReader(entry.OpenReader()))
                            {
                                var el = XElement.Parse(sr.ReadToEnd());
                                var newWOFullText = new NewWO(_woDataSource);
                                if (newWOFullText.Parse(el))
                                {
                                    insert(newWOFullText, sourcePath, entry.FileName);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(sourcePath);
                            _log.Error(entry.FileName);
                            _log.Error(e.Message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(sourcePath);
                _log.Error(e.Message);
            }
        }

        private void insert(NewWO newWO, string zipPath, string xmlPath)
        {
            Conn.Query(@"
INSERT INTO RawData.NewWO
(
    [pn],[apn],[pd],[apd],[kindCode],[docRoot],[doc],[vol],[zipPath],[xmlPath]
)
VALUES
(
    @pn, @apn, @pd, @apd, @kindCode, @docRoot, @doc, @vol, @zipPath, @xmlPath
)", new
            {
                pn = newWO.PN,
                apn = newWO.APN,
                pd = newWO.PD,
                apd = newWO.APD,
                kindCode = newWO.KindCode,
                docRoot = newWO.El.Name.ToString(),
                doc = newWO.El.ToString(),
                vol = NewWO.FormatVol(_woDataSource, zipPath, xmlPath),
                zipPath = zipPath.Replace(@"\\per7204\WIPOData\", string.Empty),
                xmlPath = xmlPath
            });
        }
    }
}