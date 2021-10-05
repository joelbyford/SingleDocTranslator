using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using Azure;
using Azure.AI.Translation.Document;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;



namespace SingleDocTranslator.Controllers
{
    [ApiController]
    [Route("[controller]")]

    public class TranslateDocumentController : ControllerBase
    {
        private readonly IConfiguration Configuration;

        //Public and Default Constructor
        public TranslateDocumentController(IConfiguration configuration)
        {
            //_logger = logger;
            Configuration = configuration;
        }
        /***************************************************
        - HTTP Post Handler
        - - toLang = 'es', 'de', etc.
        - - localPathToFile = URL Encoded path <-- TEMP
        - - Change to accept a file stream
        ****************************************************/
        [HttpPost]
        public async Task<ContentResult> Post([FromQuery]string toLang, [FromQuery]string localPathToFile)
        {
            // Generate a unique folder name
            // https://docs.microsoft.com/en-us/dotnet/api/system.guid.newguid?view=net-5.0
            Guid guid = Guid.NewGuid();
            string folderName = guid.ToString();

            // Send the binary document to Blob in the unique folder
            // TODO: obtain this as a file stream from the POST [FromBody] instead of a file path
            // The following is an example for both the calling webapp and the webapi
            // https://karthiktechblog.com/aspnetcore/how-to-upload-a-file-with-net-core-web-api-3-1-using-iformfile
            uploadFileToBlob(folderName, localPathToFile);


            // Call the translator funciton with the uniqiue folder name
            // TODO: Verify the toLang is a supported language.
            DocumentStatusResult document = await translatePath(folderName, toLang);

            if (document.Status == DocumentTranslationStatus.Succeeded)
            {
                // Generate a temporary Read-Only URI for the new document
                Uri sasUri = generateSasUri(document.TranslatedDocumentUri);

                return new ContentResult
                {
                    //Content = document.TranslatedDocumentUri.OriginalString,
                    Content = sasUri.OriginalString,
                    ContentType = "text/plain",
                    StatusCode = 200
                };
            }
            else
            {
                return new ContentResult
                {
                    Content = document.Error.Message,
                    ContentType = "text/plain",
                    StatusCode = 400
                };
            }
        }

        /******************************************
        - uploadFileToBlob Method
        - Example from: https://docs.microsoft.com/en-us/dotnet/api/overview/azure/storage.blobs-readme#uploading-a-blob
        ******************************************/
        private void uploadFileToBlob(string folderName, string localPathToFile)
        {
            // TODO: Try/Catch all of this

            // Create a BlobContainerClient with the storage account name and the key capable of signing certificates
            string storageAccountName = Configuration["AppSettings:BlobSourceStorageAccountName"];
            string storageAccountKey = Configuration["AppSettings:BlobSourceStorageAccountKey"];

            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
            Uri blobContainerUri = new Uri(Configuration["AppSettings:BlobSourceContainerUri"]);
            BlobContainerClient blobContainerClient = new BlobContainerClient(blobContainerUri, credential);

            // Get a reference to a future blob prefixed by the unique folder name
            BlobClient blob = blobContainerClient.GetBlobClient(folderName + "/SanFranciscoBrochure.pdf");

            // TODO: Uplod a file stream passed into the function instead of a local file
            //blob.Upload(FileStream);
            blob.Upload(localPathToFile);

            // TODO: Handle errors in return results
            return;

        }

        /*****************************************
        - translatePath Method
        - Example from: https://docs.microsoft.com/en-us/azure/cognitive-services/translator/document-translation/client-sdks?tabs=csharp
        *****************************************/
        private async Task<DocumentStatusResult> translatePath(string szPath, string toLang)
        {
            Uri sourceUri = new Uri(Configuration["AppSettings:BlobSourceSasUri"]);
            Uri targetUri = new Uri(Configuration["AppSettings:BlobDestSasUri"]);

            DocumentTranslationClient client = new DocumentTranslationClient(
                new Uri(Configuration["AppSettings:TranslatorEndpoint"]), 
                new AzureKeyCredential(Configuration["AppSettings:TranslatorKey"]));
            
            TranslationSource src = new TranslationSource(sourceUri);
            src.Prefix = szPath;

            var targets = new List<TranslationTarget>();
            //TODO: Need to verify the toLang is a supported langauge before the next call
            TranslationTarget target = new TranslationTarget(targetUri, toLang);
            targets.Add(target);

            DocumentTranslationInput input = new DocumentTranslationInput(src, targets);

            DocumentTranslationOperation operation = await client.StartTranslationAsync(input);

            await operation.WaitForCompletionAsync();

            // API assumes a collection of documents (not just one)
            var documents = operation.GetValues();
            // We only have 1 so, get the first one
            var document = documents.First<DocumentStatusResult>();

            return document;
        }
        
        /****************************************
        - generateSasUri Method
        - Example from: https://docs.microsoft.com/en-us/azure/storage/common/storage-account-sas-create-dotnet?tabs=dotnet
        ****************************************/

        private Uri generateSasUri(Uri blobUri)
        {
            // Create a BlobClient with the storage account name and the key capable of signing certificates
            string storageAccountName =  Configuration["AppSettings:BlobSourceStorageAccountName"];
            string storageAccountKey = Configuration["AppSettings:BlobSourceStorageAccountKey"];

            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
            BlobClient blobClient = new BlobClient(blobUri, credential);
            

            // Check whether this BlobClient object has been authorized with Shared Key.
            if (blobClient.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one hour.
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = blobClient.BlobContainerName,
                    BlobName = blobClient.Name,
                    Resource = "b"
                };
                
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
                //Console.WriteLine("SAS URI for blob is: {0}", sasUri);

                return sasUri;
            }
            else
            {
                Console.WriteLine(@"BlobClient must be authorized with Shared Key 
                                credentials to create a service SAS.");
                return null;
            }
        }

    }
}
