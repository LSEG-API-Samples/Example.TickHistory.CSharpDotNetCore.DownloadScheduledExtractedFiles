using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using DownloadScheduledExtractedFiles_dotnetcore.Types;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace DownloadScheduledExtractedFiles_dotnetcore
{
    class DSSClient
    {
        private string token;
        private string dssURI = "https://selectapi.datascope.lseg.com/RestApi/v1/";
        private string authEndpoint = "Authentication/RequestToken";
        private string scheduleByNameEndpoint = "Extractions/ScheduleGetByName(ScheduleName='{0}')";
        private string scheduleEndpoint = "Extractions/Schedules";
        private string lastExtractionEndpoint = "Extractions/Schedules('{0}')/LastExtraction";
        private string allFilesEndpoint = "Extractions/ReportExtractions('{0}')/Files";
        private string fileEndpoint = "Extractions/ReportExtractions('{0}')/";
        private string downloadFileEndpoint = "Extractions/ExtractedFiles('{0}')/$value";
        public void Login(string dssUsername, string dssPassword)
        {
            Credentials cred = new Credentials { Username = dssUsername, Password = dssPassword };
            string credStr = JsonConvert.SerializeObject(new { Credentials=cred}, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            using (HttpClient client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, new Uri(dssURI+authEndpoint));
                request.Headers.Add("Prefer", "respond-async");

                request.Content = new StringContent(credStr);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response =  client.SendAsync(request).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    token = (string)JObject.Parse(jsonData)["value"];
                    Console.WriteLine($"Get Token {token}");
                }
                else
                {
                    throw new Exception(String.Format("\nUnable to Login to Tick Historical Server\n {0} {1}", response.StatusCode.ToString(), response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
                }
                
            }
            
        }
        public void ListAllSchedules()
        {
            Console.WriteLine("\nAvailable Schedules:");
            using (HttpClient client = new HttpClient())
            {

                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(dssURI + scheduleEndpoint));
                request.Headers.Add("Prefer", "respond-async");
                request.Headers.Add("Authorization", "Token " + token);


                var response = client.SendAsync(request).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    //Console.WriteLine(jsonData);
                    foreach(var obj in JObject.Parse(jsonData)["value"]){

                        if ((string)obj["Trigger"]["@odata.type"] != "#DataScope.Select.Api.Extractions.Schedules.ImmediateTrigger")
                        {
                            Console.WriteLine($"-\t {obj["Name"]}: {(string)obj["Trigger"]["@odata.type"]}");
                        }
                    }
                    
                }
                else
                {
                    throw new Exception(String.Format("\nAn exception in ListAllSchedule:\n {0} {1}", response.StatusCode.ToString(), response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
                }

            }
        }
        public Extraction    GetLastExtraction(Schedule schedule)
        {
            if(schedule == null)
            {
                throw new Exception(String.Format("\nSchedule is null"));
            }

            using (HttpClient client = new HttpClient())
            {

                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(dssURI + string.Format(lastExtractionEndpoint, schedule.ScheduleId)));
                request.Headers.Add("Prefer", "respond-async");
                request.Headers.Add("Authorization", "Token " + token);


                var response = client.SendAsync(request).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var extraction = new Extraction();
                    var obj = JObject.Parse(jsonData);
                    extraction.ReportExtractionId = (string)obj["ReportExtractionId"];
                    extraction.Status = (string)obj["Status"];
                    extraction.ExtractionDateUtc = (string)obj["ExtractionDateUtc"];

                    return extraction;
                }

                else
                {
                    throw new Exception(String.Format("\nAn exception in GetLastExtraction: {0}\n {1} {2}", schedule.ScheduleName, response.StatusCode.ToString(), response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
                }

            }
        }
        public void DownloadFile(ExtractFile file, bool aws)
        {
            using (HttpClient client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(dssURI + string.Format(downloadFileEndpoint, file.ExtractedFileId)));
                request.Headers.Add("Prefer", "respond-async");
                request.Headers.Add("Authorization", "Token " + token);
                if (file.ExtractedFileName.EndsWith("gz"))
                {
                    request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                    request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                }
                if (aws)
                    request.Headers.Add("X-Direct-Download", "True");

                var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
               
                if (response.IsSuccessStatusCode)
                {

                    Console.WriteLine($"{file.ExtractedFileName} has been created on the machine.");
                    using (var fileStream = File.Create(file.ExtractedFileName))
                    {
                        Console.WriteLine($"Downloading a file ...");
                        using (Stream streamToReadFrom = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        {
                            
                            //response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
                            streamToReadFrom.CopyToAsync(fileStream).GetAwaiter().GetResult();
                            
                        }
                        
                    }
                    Console.WriteLine($"Download completed.");
                }
                else
                {

                }

                
            }
        }
    
        public List<ExtractFile> GetAllFiles(Extraction extraction)
        {
            if (extraction == null)
            {
                throw new Exception(String.Format("\nReport Extraction is null"));
            }
            List<ExtractFile> fileList = new List<ExtractFile>();
            using (HttpClient client = new HttpClient())
            {

                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(dssURI + string.Format(allFilesEndpoint, extraction.ReportExtractionId)));
                request.Headers.Add("Prefer", "respond-async");
                request.Headers.Add("Authorization", "Token " + token);


                var response = client.SendAsync(request).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    foreach (var obj in JObject.Parse(jsonData)["value"])
                    {
                        var tmp = new ExtractFile();

                        tmp.ExtractedFileId = (string)obj["ExtractedFileId"];
                        tmp.ExtractedFileName = (string)obj["ExtractedFileName"];
                        tmp.FileType = (string)obj["FileType"];
                        tmp.Size = (uint)obj["Size"];
                        fileList.Add(tmp);
                    }
                    return fileList;
                }
                else
                {
                    throw new Exception(String.Format("\nAn exception in GetAllFiles: {0}\n {1} {2}", extraction.ReportExtractionId, response.StatusCode.ToString(), response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
                }

            }
        }

        public ExtractFile GetFile(Extraction extraction, string fileType)
        {
            if (extraction == null)
            {
                throw new Exception(String.Format("\nReport Extraction is null"));
            }
            Uri uri = null;
            switch (fileType)
            {
                case "note":
                    uri = new Uri(dssURI + string.Format(fileEndpoint, extraction.ReportExtractionId) + "NotesFile");
                    
                    break;
                case "ric":
                    uri = new Uri(dssURI + string.Format(fileEndpoint, extraction.ReportExtractionId) + "RicMaintenanceFile");
                   
                    break;
                case "data":
                    uri = new Uri(dssURI + string.Format(fileEndpoint, extraction.ReportExtractionId) + "FullFile");
                    
                    break;

                default:
                    throw new Exception(String.Format($"\nFile Type {fileType} is unknown."));


            }
            using (HttpClient client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Add("Prefer", "respond-async");
                request.Headers.Add("Authorization", "Token " + token);
                var response = client.SendAsync(request).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var file = new ExtractFile();
                    if (string.IsNullOrEmpty(jsonData))
                    {
                        
                        throw new Exception($"\nA {fileType} file type is not available on the server.");
                    }
                    var obj = JObject.Parse(jsonData);
                    file.ExtractedFileId = (string)obj["ExtractedFileId"];
                    file.ExtractedFileName = (string)obj["ExtractedFileName"];
                    file.FileType = (string)obj["FileType"];
                    file.Size = (uint)obj["Size"];
                    return file;

                }
                else
                {
                    throw new Exception(String.Format("\nAn exception in GetFile: {0}\n {1} {2}", extraction.ReportExtractionId, response.StatusCode.ToString(), response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));


                }

            }

               
        }
        public Schedule GetScheduleByName(string scheduleName)
        {

            using (HttpClient client = new HttpClient())
            {
                
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(dssURI + string.Format(scheduleByNameEndpoint, scheduleName)));
                request.Headers.Add("Prefer", "respond-async");
                request.Headers.Add("Authorization", "Token "+token);
               
                
                var response = client.SendAsync(request).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var schedule = new Schedule();
                    var obj = JObject.Parse(jsonData);
                    schedule.ScheduleId = (string)obj["ScheduleId"];
                    schedule.ScheduleName = (string)obj["Name"];
                    schedule.Trigger = (string)obj["Trigger"]["@odata.type"];                    
                    return schedule;
                }
                else
                {
                    ListAllSchedules();
                    throw new Exception(String.Format("\nAn exception in GetScheduleByName: {0}\n {1} {2}",scheduleName, response.StatusCode.ToString(), response.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
                }

            }
        }

        
    }
}

