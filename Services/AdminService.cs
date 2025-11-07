/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * AdminService
 * File Purpose:
 * This is a service that handles general admin methods
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
using J_Tutors_Web_Platform.Models.Admins;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.Models.Subjects;
using Microsoft.Data.SqlClient;
using J_Tutors_Web_Platform.ViewModels;
using System.Security.Claims;

namespace J_Tutors_Web_Platform.Services
{
    public class AdminService
    {
        private readonly string _connectionString;
        public AdminService(string connectionString)
        {
            _connectionString = connectionString; //initializing connection string
        }

        public int GetAdminID(string Username)
        {
            const string sql = "select AdminId from Admins where Username = @Username"; //gets admin id from admin table where username matches param
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username); //adding parameter to prevent sql injection

            constring.Open();

            var id = (int)cmd.ExecuteScalar(); //ExecuteScalar returns first column of first row in result

            constring.Close();
            return id;
        }

        public int GetUserID(string Username)
        {
            const string sql = "select UserId from Users where Username = @Username"; //gets user id from admin table where username matches param
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username); //adding parameter to prevent sql injection

            constring.Open();

            var id = (int)cmd.ExecuteScalar();

            constring.Close();
            return id;
        }

        public bool IsLeaderboardVisible(string Username)
        {
            const string sql = "select LeaderboardVisible from Users where Username = @Username"; // checks to see if a specific user has a leaderboard visibility enabled or disabled
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username); //adding parameter to prevent sql injection

            constring.Open();

            var ans = (bool)cmd.ExecuteScalar();

            constring.Close();
            return ans;
        }

        public int GetTotalPoints(string Username)
        {
            int id = GetUserID(Username);

            const string sql = "select SUM(Amount) from PointsReceipt where UserID = @UserID and Type = 'Earned'"; //adds all points earned by a specific user
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", id); //adding parameter to prevent sql injection

            constring.Open();

            var totalPoints = cmd.ExecuteScalar();

            if (totalPoints == null) //checks if result from sql query is null
                totalPoints = 0;

            if (totalPoints == DBNull.Value) //checks if result from sql query is db null which is different from c# null and causes a bunch of issues if not handled (stops page from loading)
                totalPoints = 0;

            constring.Close();

            return (int)totalPoints;
        }

        public int GetPointsSpent(string Username)
        {
            int id = GetUserID(Username);

            const string sql = "select SUM(Amount) from PointsReceipt where UserID = @UserID and Type = 'Spent'"; //adds all receipts from specific user where points were spent, so total points spent
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", id); //params to prevent sql injection

            constring.Open();

            var totalPoints = cmd.ExecuteScalar();

            if (totalPoints == null) //checks if result from Executescalar was null
                totalPoints = 0;

            if (totalPoints == DBNull.Value) //checks if result from Executescalar was db null which is not a regular null
                totalPoints = 0;

            constring.Close();

            return (int)totalPoints;
        }

        public List<UserDirectoryRow> GetAllUsers(string Username)
        {
            var userList = new List<UserDirectoryRow>();
            int totalPoints;
            int pointsSpent;
            int currentPoints;
            bool isVisible;
            int unPaidSessions;
            double unPaidAmount;
            DateTime lastActivity;

            const string sql = "select * from Users";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();
            using SqlDataReader reader = cmd.ExecuteReader();

            //reading through returned data
            while (reader.Read())
            {
                totalPoints = GetTotalPoints(reader["Username"].ToString());
                pointsSpent = GetPointsSpent(reader["Username"].ToString());
                currentPoints = totalPoints - pointsSpent;

                isVisible = IsLeaderboardVisible(reader["Username"].ToString());

                unPaidSessions = 0; //add funcionality later after sessions can be booked
                unPaidAmount = 0.0; //add funcionality later after sessions can be booked

                lastActivity = DateTime.Now;

                userList.Add(new UserDirectoryRow
                {
                    Username = reader["Username"].ToString()!,
                    UnpaidSessions = unPaidSessions,
                    UnpaidAmount = unPaidAmount,
                    CurrentPoints = currentPoints,
                    TotalPoints = totalPoints,
                    LastActivity = lastActivity,
                    LeaderboardVisible = isVisible
                });
            }

            constring.Close();
            return userList;
        }

        // ==================================================================================================
        //  QUOTATIONS
        //  PURPOSE:
        //          - Management of pricing rules for subjects
        // ==================================================================================================

        public List<Subject> GetAllSubjects()
        {
            var list = new List<Subject>();
            const string sql = "SELECT * FROM Subjects ORDER BY SubjectName";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            con.Open();

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Subject
                {
                    SubjectID = Convert.ToInt32(r["SubjectID"]),
                    SubjectName = Convert.ToString(r["SubjectName"]) ?? "",
                    IsActive = Convert.ToBoolean(r["IsActive"])
                });
            }
            return list;
        }

        public int CreateSubject(string subjectName)
        {
            if (string.IsNullOrWhiteSpace(subjectName)) throw new ArgumentException("Subject name required.");

            const string sql = @"INSERT INTO Subjects (SubjectName, IsActive) VALUES (@name, 1); SELECT SCOPE_IDENTITY();";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@name", subjectName.Trim());
            con.Open();

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public int DeleteSubject(int subjectId)
        {
            // If FK to PricingRule has no CASCADE, this preserves referential integrity.
            const string sql = @"DELETE FROM PricingRule WHERE SubjectID = @id; DELETE FROM Subjects WHERE SubjectID = @id;";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", subjectId);
            con.Open();

            return cmd.ExecuteNonQuery();
        }

        public int ToggleSubjectActive(int subjectId)
        {
            const string sql = @"UPDATE Subjects SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE SubjectID = @id;";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", subjectId);
            con.Open();
            return cmd.ExecuteNonQuery();
        }


        public PricingRule? GetPricingForSubject(int subjectId)
        {
            const string sql = @"SELECT TOP 1 * FROM PricingRule WHERE SubjectID = @sid";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@sid", subjectId);
            con.Open();

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new PricingRule
            {
                PricingRuleID = Convert.ToInt32(r["PricingRuleID"]),
                SubjectID = Convert.ToInt32(r["SubjectID"]),
                AdminID = Convert.ToInt32(r["AdminID"]),
                HourlyRate = Convert.ToDecimal(r["HourlyRate"]),
                MinHours = Convert.ToDecimal(r["MinHours"]),
                MaxHours = Convert.ToDecimal(r["MaxHours"]),
                MaxPointDiscount = Convert.ToDecimal(r["MaxPointDiscount"])
            };
        }

        public void UpsertPricing(int subjectId, int adminId, decimal hourlyRate, decimal minHours, decimal maxHours, decimal maxPointDiscount)
        {
            if (minHours <= 0 || maxHours <= 0 || maxHours < minHours) throw new ArgumentException("Invalid hour range.");
            if (hourlyRate < 0) throw new ArgumentException("Hourly rate must be >= 0.");
            if (maxPointDiscount < 0) throw new ArgumentException("Max points discount must be >= 0.");

            const string sql = @"IF EXISTS (SELECT 1 FROM PricingRule WHERE SubjectID = @sid) BEGIN UPDATE PricingRule SET AdminID = @aid, HourlyRate = @hr, MinHours = @minh, MaxHours = @maxh, MaxPointDiscount = @mpd WHERE SubjectID = @sid; END ELSE BEGIN INSERT INTO PricingRule (SubjectID, AdminID, HourlyRate, MinHours, MaxHours, MaxPointDiscount) VALUES (@sid, @aid, @hr, @minh, @maxh, @mpd); END";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@sid", subjectId);
            cmd.Parameters.AddWithValue("@aid", adminId);
            cmd.Parameters.AddWithValue("@hr", hourlyRate);
            cmd.Parameters.AddWithValue("@minh", minHours);
            cmd.Parameters.AddWithValue("@maxh", maxHours);
            cmd.Parameters.AddWithValue("@mpd", maxPointDiscount);
            con.Open();

            cmd.ExecuteNonQuery();
        }

        // ==================================================================================================
        //  THEME PREFERENCE
        //  CONTROLLER: AdminController
        //  PURPOSE:
        //          - Change theme preference in db for admin.
        // ==================================================================================================

        public async Task ChangeTheme(string Username, string pref) 
        {
            await using var constring = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(
                "UPDATE Users SET ThemePreference=@p WHERE Username=@u", constring);
            cmd.Parameters.AddWithValue("@p", (object)pref ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", Username);

            await constring.OpenAsync();

            await cmd.ExecuteNonQueryAsync();

            Console.WriteLine("Theme updated successfully");
        }
    }
}
