# Purpose
The Azure Document Translator is a purely batch process which expects to loop through a series of documents in an Azure Storage Blob, create translated documents, and store those documents back in another folder within the same Blob store.  

This is a simple doitnet C# project which automates sending a single file to Blob storage then automatically triggering the batch-oriented document translator to translate the single document.  

# Limitations
Currently only shows an example of sending a URL to a single file, however replacing the URL with a MIME-based file stream sent via HTTP/S may be replaced where commented.   

# Warranties
None.  Developed purely as an example Proof of Concept.  Please use at your own risk. 
