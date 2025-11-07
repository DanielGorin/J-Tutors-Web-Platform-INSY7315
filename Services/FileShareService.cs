/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * FileShareService
 * File Purpose:
 * This is a service which holds methods relating to the azure file share
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
using Azure.Storage.Files.Shares;
using J_Tutors_Web_Platform.Models.AppFiles;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace J_Tutors_Web_Platform.Services
{
    public sealed class FileShareService
    {
        private readonly ShareClient _share;
        private readonly string _connectionString;

        // --------------------------------Univeral methods---------------------------------
        
        //--------------------------
        // fetches admin id from username
        // -------------------------
        public int GetAdminID(string Username)
        {
            const string sql = "select AdminId from Admins where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username);

            constring.Open();

            var id = (int)cmd.ExecuteScalar();

            constring.Close();
            return id;
        }

        //--------------------------
        // fetches user id from username
        // -------------------------
        public int GetUserID(string Username)
        {
            const string sql = "select UserId from Users where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username);

            constring.Open();

            var id = (int)cmd.ExecuteScalar();

            constring.Close();
            return id;
        }

        //--------------------------
        // fetches user username from id
        // -------------------------
        public string GetUsername(int UserID)
        {
            const string sql = "select Username from Users where UserID = @UserID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", UserID);

            constring.Open();

            var username = (string)cmd.ExecuteScalar();

            constring.Close();
            return username;
        }

        //--------------------------
        // fetches count of users who have access to a specific file
        // -------------------------
        public int GetUserCount(int FileID)
        {
            const string sql = "select count(*) from FileAccess where FileID = @FileID";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileID", FileID);

            constring.Open();

            var count = (int)cmd.ExecuteScalar();

            constring.Close();
            return count;
        }

        //----------------------------------------------------------------------------------------------------------------------------

        //--------------------------
        // establishing connection to fileshare
        // -------------------------
        public FileShareService(IConfiguration config, string connectionString)
        {
            var cs = config["AzureStorage:ConnectionString"]
                  ?? config.GetConnectionString("StorageAccount");
            var shareName = config["AzureStorage:FileShareName"] ?? "jtutors-fileshare";
            if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Missing storage connection string.");
            if (string.IsNullOrWhiteSpace(shareName)) throw new InvalidOperationException("Missing file share name.");

            // Pin to a stable API version to avoid odd 405s from future versions
            var opts = new ShareClientOptions(ShareClientOptions.ServiceVersion.V2023_11_03);

            _share = new ShareClient(cs.Trim(), shareName.Trim(), opts);
            _share.CreateIfNotExists(); // idempotent

            _connectionString = connectionString;
        } 

        // ROOT ONLY
        public async Task<string> UploadAsync(string Username, Stream content, long length, string fileName, CancellationToken ct = default)
        {
            Console.WriteLine("Uploading file to File Share: " + fileName);

            if (length <= 0) throw new ArgumentException("length must be > 0");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName required");

            // Ensure share exists (constructor already does CreateIfNotExists; this is just defensive)
            await _share.CreateIfNotExistsAsync(cancellationToken: ct);

            var root = _share.GetRootDirectoryClient(); // root exists automatically with the share
            var file = root.GetFileClient(fileName.Trim());

            await file.CreateAsync(length, cancellationToken: ct);
            await file.UploadRangeAsync(new Azure.HttpRange(0, length), content, cancellationToken: ct);

            int AdminID = GetAdminID(Username);

            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            provider.TryGetContentType(fileName, out var contentType);

            const string sql = "insert into Files (AdminID, FileName, ContentType) " +
                               "values (@AdminID, @FileName, @ContentType)";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@AdminID", AdminID);
            cmd.Parameters.AddWithValue("@FileName", fileName);
            cmd.Parameters.AddWithValue("@ContentType", contentType ?? "application/octet-stream");

            constring.Open();

            cmd.ExecuteNonQuery();

            constring.Close();

            Console.WriteLine("Inserting file record into database: " + fileName + AdminID);

            return file.Name;
        }

        //--------------------------
        // fetch list of file share details from sql database
        // -------------------------
        public List<FileShareRow> GetFileShareRows(string Username) 
        {
            var fsrList = new List<FileShareRow>();
            string fileName;
            string adminUsername = Username;
            int userCount;
            int fileID;

            const string sql = "select * from Files";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                fileName = reader.GetString(2);
                fileID = reader.GetInt32(0);
                userCount = GetUserCount(fileID);

                fsrList.Add(new FileShareRow
                {
                    FileName = fileName,
                    AdminUsername = Username,
                    UserCount = userCount
                });
            }

            constring.Close();
            return fsrList;
        }

        //--------------------------
        // fetch list of files from file share
        // -------------------------
        public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
        {
            await _share.CreateIfNotExistsAsync(cancellationToken: ct);

            var root = _share.GetRootDirectoryClient(); // no CreateIfNotExists on root
            var list = new List<string>();

            await foreach (var item in root.GetFilesAndDirectoriesAsync(cancellationToken: ct))
                if (!item.IsDirectory) list.Add(item.Name);

            return list;
        }

        //--------------------------
        // downloads file from the file share
        // -------------------------
        public async Task<Stream> DownloadAsync(string fileName, CancellationToken ct = default)
        {
            await _share.CreateIfNotExistsAsync(cancellationToken: ct);

            var root = _share.GetRootDirectoryClient();
            var file = root.GetFileClient((fileName ?? "").Trim());
            var resp = await file.DownloadAsync(cancellationToken: ct);

            var ms = new MemoryStream();
            await resp.Value.Content.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }

        public async Task<Stream> DeleteAsync(string fileName, CancellationToken ct = default)
        {
            await _share.CreateIfNotExistsAsync(cancellationToken: ct);

            var root = _share.GetRootDirectoryClient();
            var file = root.GetFileClient((fileName ?? "").Trim());
            await file.DeleteAsync(cancellationToken: ct);

            DeleteFile(fileName);

            return Stream.Null;
        }

        public void DeleteFile(string FileName)
        {
            DeleteFileAccess(GetFileID(FileName));

            const string sql = "delete from Files where FileName = @FileName";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileName", FileName);
            
            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();
            

        }

        public void DeleteFileAccess(int FileID)
        {
            const string sql = "delete from FileAccess where FileID = @FileID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileID", FileID);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();

        }

        public int GetFileID(string FileName) 
        {
            int id;

            const string sql = "select FileID from Files where FileName = @FileName";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileName", FileName);

            constring.Open();

            id = (int)cmd.ExecuteScalar();

            constring.Close();

            return id;
        }

        public string GetFileName(int FileID)
        {
            string fileName;

            const string sql = "select FileName from Files where FileID = @FileID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileID", FileID);

            constring.Open();
            fileName = (string)cmd.ExecuteScalar();
            constring.Close();

            return fileName;
        }

        public List<FileShareAccessRow> GetFileShareAccessRows(string FileName) 
        {
            var fsarList = new List<FileShareAccessRow>();
            int fileID = GetFileID(FileName);
            string userName;

            const string sql = "select * from FileAccess where FileID = @FileID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileID", fileID);

            constring.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                userName = GetUsername(reader.GetInt32(2));

                fsarList.Add(new FileShareAccessRow
                {
                    FileAccessID = reader.GetInt32(0),
                    FileID = fileID,
                    FileName = FileName,
                    UserID = reader.GetInt32(2),
                    Username = userName,
                    StartDate = reader.GetDateTime(3),
                    EndDate = reader.GetDateTime(4)
                });
            }

            constring.Close();

            return fsarList;
        }

        public void AddFileAccess(int FileID, string Username, DateTime StartDate, DateTime EndDate) 
        {
            var userID = GetUserID(Username);

            const string sql = "insert into FileAccess (FileID, UserID, StartDate, EndDate) values (@FileID, @UserID, @StartDate, @EndDate)";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileID", FileID);
            cmd.Parameters.AddWithValue("@UserID", userID);
            cmd.Parameters.AddWithValue("@StartDate", StartDate);
            cmd.Parameters.AddWithValue("@EndDate", EndDate);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();
        }

        public void UpdateFileAccess(int FileId, int UserID, DateOnly StartDate, DateOnly EndDate)
        {
            const string sql = "update FileAccess set StartDate = @StartDate, EndDate = @EndDate where FileID = @FileID and UserID = @UserID";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileID", FileId);
            cmd.Parameters.AddWithValue("@UserID", UserID);
            cmd.Parameters.AddWithValue("@StartDate", StartDate);
            cmd.Parameters.AddWithValue("@EndDate", EndDate);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();
        }

        public void DeleteAccess(int FileID, int UserID) 
        {
             const string sql = "delete from FileAccess where FileID = @FileID and UserID = @UserID";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@FileID", FileID);
            cmd.Parameters.AddWithValue("@UserID", UserID);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();
        }

        public List<FileShareAccessRow> GetUserFileShares(string Username) 
        {
            List<FileShareAccessRow> fsarList = new List<FileShareAccessRow>();
            int userID = GetUserID(Username);
            
            Console.WriteLine("Getting file shares for userID: " + userID);

            const string sql = "select * from FileAccess where UserID = @UserID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", userID);

            constring.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string fileName = GetFileName(reader.GetInt32(1));

                fsarList.Add(new FileShareAccessRow
                {
                    FileAccessID = reader.GetInt32(0),
                    FileID = reader.GetInt32(1),
                    FileName = fileName,
                    UserID = userID,
                    StartDate = reader.GetDateTime(3),
                    EndDate = reader.GetDateTime(4)
                });
            }

            constring.Close(); 
            
            return fsarList;

        }

    }
}
