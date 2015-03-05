﻿using SeafClient.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SeafClient.Requests
{
    /// <summary>
    /// Request used to upload files    
    /// </summary>
    /// <see cref="https://cloud.seafile.com/f/1b0ade6edc/"/>
    public class UploadRequest : SessionRequest<bool>
    {
        Action<float> UploadProgress;

        public string UploadUri { get; set; }

        public string TargetDirectory {get; set;}

        List<UploadFileInfo> files = new List<UploadFileInfo>();        

        public List<UploadFileInfo> Files
        {
            get
            {
                return files;
            }
        }

        public override string CommandUri
        {
            get { return UploadUri; }
        }

        public override HttpAccessMethod HttpAccessMethod
        {
            get { return HttpAccessMethod.Custom; }
        }

        /// <summary>
        /// Create an upload request for a single file
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="uploadUri"></param>
        /// <param name="filename"></param>
        /// <param name="fileContent"></param>
        /// <param name="progressCallback"></param>
        public UploadRequest(string authToken, string uploadUri, string targetDirectory, string filename, Stream fileContent, Action<float> progressCallback)            
            : this(authToken, uploadUri, targetDirectory, progressCallback, new UploadFileInfo(filename, fileContent))
        {            
            // --
        }

        /// <summary>
        /// Create an upload request for multiple file
        /// </summary>
        /// <param name="authToken"></param>
        /// <param name="uploadUri"></param>
        /// <param name="filename"></param>
        /// <param name="fileContent"></param>
        /// <param name="progressCallback"></param>
        public UploadRequest(string authToken, string uploadUri, string targetDirectory, Action<float> progressCallback, params UploadFileInfo[] uploadFiles)
            : base(authToken)
        {
            UploadUri = uploadUri;
            UploadProgress = progressCallback;
            TargetDirectory = targetDirectory;

            files.AddRange(uploadFiles);            
        }

        public override async Task<bool> ParseResponseAsync(HttpResponseMessage msg)
        {
            string content = await msg.Content.ReadAsStringAsync();
            return !String.IsNullOrEmpty(content);
        }

        public override async Task<HttpResponseMessage> SendRequestCustomizedAsync(string serverUri)
        {
            using (HttpClient client = new HttpClient())
            {
                string boundary = "Upload---------" + Guid.NewGuid().ToString();

                var request = new MultipartFormDataContent(boundary);                

                // Add files to upload to the request
                foreach (var f in Files)
                {
                    //var fileContent = new StreamContent(f.FileContent);
                    var fileContent = new ProgressableStreamContent(f.FileContent, (p) =>
                    {
                        if (UploadProgress != null)
                            UploadProgress(p);                        
                    });
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");                    
                    fileContent.Headers.TryAddWithoutValidation("Content-Disposition", String.Format("form-data; name=\"file\"; filename=\"{0}\"", f.Filename));
                    request.Add(fileContent);
                }                

                // the parent dir to upload the file to
                string tDir = TargetDirectory;
                if (!tDir.StartsWith("/"))
                    tDir = "/" + tDir;

                var dirContent = new StringContent(tDir, Encoding.GetEncoding("ASCII"));
                dirContent.Headers.ContentType = null;
                dirContent.Headers.TryAddWithoutValidation("Content-Disposition", @"form-data; name=""parent_dir""");
                request.Add(dirContent);  
                
                // the seafile-server implementation rejects the content-type if the boundary value is
                // placed inside quotes which HttpClient does, so we have to redefine the content-type without using quotes
                // remove the actual content-type which uses quotes
                request.Headers.ContentType = null;
                request.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);                

                return await client.PostAsync(new Uri(UploadUri), request);                 
                
            }     
        }
    }

    /// <summary>
    /// Information about a file which shall be uploaded
    /// </summary>
    public class UploadFileInfo
    {
        public string Filename { get; set; }
        public Stream FileContent { get; set; }

        public UploadFileInfo(string filename, Stream content)
        {
            Filename = filename;
            FileContent = content;
        }
    }
}
