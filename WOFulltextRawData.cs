using Dapper;
using Ionic.Zip;
using Ltc.WOData;
using NLog;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace App.WOXmlToRawDataDB
{
    internal class WOFulltextRawData
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();
        public List<string> SourcePathList { get; set; }
        public SqlConnection Conn { get; set; } = new SqlConnection(ConnectionStringsAndAppSettings._PatentEP_CS);
        private WOFullTextDataSource _woFullTextDataSource;

        public WOFulltextRawData(WOFullTextDataSource woFullTextDataSource)
        {
            Conn.Open();
            _woFullTextDataSource = woFullTextDataSource;
            switch (woFullTextDataSource)
            {
                case WOFullTextDataSource.FullTextXML:
                    SourcePathList = Directory.EnumerateFiles(ConnectionStringsAndAppSettings.FullTextXML_ZipsPath, "*.zip", SearchOption.AllDirectories).ToList();
                    _log.Debug($"Xml Source Path: {ConnectionStringsAndAppSettings.FullTextXML_ZipsPath}");
                    break;

                case WOFullTextDataSource.PCTFullText:
                    SourcePathList = Directory.EnumerateFiles(ConnectionStringsAndAppSettings.PCTFullText_ZipsPath, "*.zip", SearchOption.AllDirectories).ToList();
                    _log.Debug($"Xml Source Path: {ConnectionStringsAndAppSettings.PCTFullText_ZipsPath}");
                    break;

                case WOFullTextDataSource.PCTBackFileCJK:
                    SourcePathList = Directory.EnumerateFiles(ConnectionStringsAndAppSettings.PCTBackFileCJK_ZipsPath, "*.zip", SearchOption.AllDirectories).ToList();

                    _log.Debug($"Xml Source Path: {ConnectionStringsAndAppSettings.PCTBackFileCJK_ZipsPath}");
                    break;

                case WOFullTextDataSource.WipoFtp:
                    SourcePathList = Directory.EnumerateFiles(ConnectionStringsAndAppSettings.Text_ZipsPath, "*.zip", SearchOption.AllDirectories).ToList();
                    _log.Debug($"Xml Source Path: {ConnectionStringsAndAppSettings.Text_ZipsPath}");
                    break;
            }
            _log.Debug($"Xml Source File Type: .zip");
        }

        public void DataResetProcess()
        {
            switch (_woFullTextDataSource)
            {
                case WOFullTextDataSource.FullTextXML:
                case WOFullTextDataSource.PCTFullText:
                case WOFullTextDataSource.WipoFtp:
                    foreach (var sourcePath in SourcePathList)
                    {
                        SingleZipInZipProcess(sourcePath);
                    }
                    break;

                case WOFullTextDataSource.PCTBackFileCJK:
                    foreach (var sourcePath in SourcePathList)
                    {
                        SingleZipProcess(sourcePath);
                    }
                    break;
            }
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
                            using (StreamReader sr = new StreamReader(entry.OpenReader()))
                            {
                                var el = XElement.Parse(sr.ReadToEnd());
                                var newWOFullText = new NewWOFullText(_woFullTextDataSource, sourcePath, entry.FileName);
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

        public void SingleZipInZipProcess(string sourcePath)
        {
            try
            {
                var zipEntryNameList = new List<string>();
                using (var firstLevelZipFile = ZipFile.Read(sourcePath))
                {
                    foreach (var zipEntry in firstLevelZipFile.Where(x => x.FileName.ToUpper().EndsWith(".ZIP")))
                    {
                        using (var ms = new MemoryStream())
                        {
                            zipEntry.Extract(ms);
                            ms.Position = 0;
                            using (var zipFile = ZipFile.Read(ms))
                            {
                                foreach (var entry in zipFile.Where(x => x.FileName.ToUpper().EndsWith(".XML")))
                                {
                                    try
                                    {
                                        _log.Trace($"{zipEntry.FileName}");
                                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                                        using (StreamReader sr = new StreamReader(entry.OpenReader()))
                                        {
                                            var el = XElement.Parse(sr.ReadToEnd());
                                            var newWOFullText = new NewWOFullText(_woFullTextDataSource, sourcePath, zipEntry.FileName);
                                            if (newWOFullText.Parse(el))
                                            {
                                                insert(newWOFullText, sourcePath, entry.FileName);
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        _log.Error(sourcePath);
                                        _log.Error(zipEntry.FileName);
                                        _log.Error(entry.FileName);
                                        _log.Error(e.Message);
                                    }
                                }
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
        }

        private void insert(NewWOFullText newWOFullText, string zipPath, string xmlPath)
        {
            Conn.Execute(@"
INSERT INTO [_PatentEP].[RawData].[NewWOFullText]
(
    [PN]
      ,[APN]
      ,[KindCode]
      ,[DOC]
      ,[ClaimsElement]
      ,[DescriptionElement]
      ,[DrawingsElement]
      ,[ZipPath]
      ,[ZipPathInZip]
      ,[XmlPath]
      ,[Vol]
      ,[WOFullTextDataSource]
)
VALUES
(
    @pn
      ,@apn
      ,@kindCode
      ,@doc
      ,@claimsElement
      ,@descriptionElement
      ,@drawingsElement
      ,@zipPath
      ,@zipPathInZip
      ,@xmlPath
      ,@vol
      ,@woFullTextDataSource
)", new
            {
                pn = newWOFullText.PN,
                apn = newWOFullText.APN,
                kindCode = newWOFullText.KindCode,
                doc = newWOFullText.DOC.ToString(),
                claimsElement = newWOFullText.ClaimsElement != null ? newWOFullText.ClaimsElement.ToString() : null,
                descriptionElement = newWOFullText.DescriptionElement != null ? newWOFullText.DescriptionElement.ToString() : null,
                drawingsElement = newWOFullText.DrawingsElement != null ? newWOFullText.DrawingsElement.ToString() : null,
                zipPath = zipPath.Replace(@"\\per7204\WIPOData\", string.Empty),
                zipPathInZip = newWOFullText.ZipPathInZip,
                xmlPath = xmlPath,
                vol = newWOFullText.Vol,
                woFullTextDataSource = _woFullTextDataSource.ToString()
            });
        }
    }
}