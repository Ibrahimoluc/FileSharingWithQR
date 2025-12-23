# FileSharingWithQR - Backend API

This project is an ASP.NET Core Web API application that allows users to share files from their local machine or Google Drive by generating temporary, **time-limited QR codes**.

## 🚀 Features

- **Hybrid File Source:** Supports both local file uploads and Google Drive integration.
- **Snapshot Architecture:** Files shared from Google Drive are downloaded to the server instantly upon sharing. This ensures the file remains accessible to the QR code scanner even if the original owner goes offline or their Google token expires.
- **Time-Limited Access (Expiration):** Users can define a specific validity period (e.g., 15 minutes, 1 hour). Once the time expires, the QR code becomes invalid.
- **Secure Tokenization:** Access permissions and expiration times (`EndTime`) are embedded within a JWT (JSON Web Token).
- **Validations:**
  - Maximum file size limit: **15 MB**
  - Supported formats: PDF, DOCX, XLSX, PPTX, JPEG, PNG.
- **Docker Ready:** configured to handle Linux file permission issues (avoiding `Access Denied` errors on volume mounts).

## 🛠 Tech Stack

- .NET 8.0 (ASP.NET Core Web API)
- Google Drive API v3
- System.IdentityModel.Tokens.Jwt
- Docker

## ⚙️ Setup & Running

### 1. Prerequisites
- .NET 8.0 SDK
- Docker Desktop (Optional, for containerized deployment)
- Google Cloud Console Project
    - A Web Client  

### 2. Configuration (`appsettings.json`)
You need to set following enviroment variables for Google Auth credentials and JWT settings:

- AllowedFrontendOrigins__0 = <Your_Frontend_Url>/DriveFiles
- WebClient2_ClientId = <CLIENTID_OF_YOUR_CLIENT_OF_GOOGLE_CLOUD_PROJECT>
- JWTKey = <YOUR_JWT_SECRET_KEY>
- WebClient2_ClientSecret = <CLIENT_SECRET_OF_YOUR_CLIENT_OF_GOOGLE_CLOUD_PROJECT>

### 3. Deploy
This project deployed at Azure Web App.
