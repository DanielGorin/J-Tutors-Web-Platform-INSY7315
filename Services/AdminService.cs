using J_Tutors_Web_Platform.Models.Admins;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Models.Users;
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
            _connectionString = connectionString;
        }
        //============================== Universal ========================================

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

        public bool IsLeaderboardVisible(string Username)
        {
            const string sql = "select LeaderboardVisible from Users where Username = @Username";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Username", Username);

            constring.Open();

            var ans = (bool)cmd.ExecuteScalar();

            constring.Close();
            return ans;
        }

        public int GetTotalPoints(string Username)
        {
            int id = GetUserID(Username);

            const string sql = "select SUM(Amount) from PointsReceipt where UserID = @UserID and Type = 'Earned'";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", id);

            constring.Open();

            var totalPoints = (int?)cmd.ExecuteScalar();

            constring.Close();

            return totalPoints ?? 0;
        }

        public int GetPointsSpent(string Username)
        {
            int id = GetUserID(Username);

            const string sql = "select SUM(Amount) from PointsReceipt where UserID = @UserID and Type = 'Spent'";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", id);

            constring.Open();

            var totalPoints = (int?)cmd.ExecuteScalar();

            constring.Close();

            return totalPoints ?? 0;
        }

        //============================== DashBoard ========================================



        //============================== Sessions & Calender ==============================

        public List<TutoringSession> GetTutoringSessions()
        {
            Console.WriteLine("Inside GetTutoringSessions method");

            var Sessionlist = new List<TutoringSession>();

            const string sql = "select * from TutoringSession";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            Console.WriteLine("before while");
            while (reader.Read())
            {
                Sessionlist.Add(new TutoringSession
                {
                    TutoringSessionID = Convert.ToInt32(reader["TutoringSessionID"]),
                    UserID = Convert.ToInt32(reader["UserID"]),
                    AdminID = Convert.ToInt32(reader["AdminID"]),
                    SubjectID = Convert.ToInt32(reader["SubjectID"]),
                    SessionDate = Convert.ToDateTime(reader["SessionDate"]),
                    DurationHours = Convert.ToDecimal(reader["DurationHours"]),
                    BaseCost = Convert.ToDecimal(reader["BaseCost"]),
                    PointsSpent = Convert.ToInt32(reader["PointsSpent"]),
                    Status = Enum.Parse<TutoringSessionStatus>(reader["Status"].ToString()!),
                    CancellationDate = reader["CancellationDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["CancellationDate"]),
                    PaidDate = reader["PaidDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["PaidDate"])
                });
            }

            Console.WriteLine("outside while");
            return Sessionlist;
        }

        public List<AvailabilityBlock> GetAvailabilityBlocks()
        {
            Console.WriteLine("Inside GetAvailabilityBlocks method");

            var avList = new List<AvailabilityBlock>();

            const string sql = "select * from AvailabilityBlock"; // add where admin id = @AdminId - not adding this as cannot login and nav to sessionandclaender yet
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                avList.Add(new AvailabilityBlock
                {
                    AvailabilityBlockID = Convert.ToInt32(reader["AvailabilityBlockID"]),
                    AdminID = Convert.ToInt32(reader["AdminID"]),
                    BlockDate = Convert.ToDateTime(reader["BlockDate"]),
                    StartTime = reader.GetTimeSpan(reader.GetOrdinal("StartTime")),
                    EndTime = reader.GetTimeSpan(reader.GetOrdinal("EndTime"))
                });
            }

            foreach (var item in avList)
            {
                Console.WriteLine($"ID: {item.AvailabilityBlockID}, Date: {item.BlockDate}, Start: {item.StartTime}, End: {item.EndTime}");
            }

            return avList;
        }

        public string CreateAvailabilitySlot(string Username, DateTime BlockDate, TimeOnly StartTime, int Duration)
        {
            int adminId = GetAdminID(Username);
            TimeSpan endTime = StartTime.ToTimeSpan().Add(new TimeSpan(0, Duration, 0));

            const string sql = "insert into AvailabilityBlock (AdminID, BlockDate, StartTime, EndTime) " +
                               "values (@AdminID, @BlockDate, @StartTime, @EndTime)";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            //adding parameters to prevent sql injection
            cmd.Parameters.AddWithValue("@AdminID", adminId);
            cmd.Parameters.AddWithValue("@BlockDate", BlockDate);
            cmd.Parameters.AddWithValue("@StartTime", StartTime);
            cmd.Parameters.AddWithValue("@EndTime", endTime);

            //executing the command
            constring.Open();
            cmd.ExecuteNonQuery();

            Console.WriteLine("Availability block created successfully");
            return "Successfully created availability block";
        }

        //============================== Users ==============================

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

        //public List<UserDirectoryRow> FilterSearch() {}

        //public List<UserDirectoryRow> SortSearch() {}

        //============================== Events ==============================



        //============================== Files ==============================



        //============================== Quotations ==============================
        // ============================== Quotations (Subjects + Pricing) ==============================

        public sealed class SubjectRow
        {
            public int SubjectID { get; set; }
            public string SubjectName { get; set; } = "";
            public bool IsActive { get; set; }
        }

        public sealed class PricingRuleRow
        {
            public int? PricingRuleID { get; set; } // null if not set yet
            public int SubjectID { get; set; }
            public int AdminID { get; set; }
            public decimal HourlyRate { get; set; }
            public decimal MinHours { get; set; }
            public decimal MaxHours { get; set; }
            public decimal MaxPointDiscount { get; set; }
        }

        // ---- Subjects ----

        public List<SubjectRow> GetAllSubjects()
        {
            var list = new List<SubjectRow>();
            const string sql = "SELECT SubjectID, SubjectName, IsActive FROM Subjects ORDER BY SubjectName";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            con.Open();

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new SubjectRow
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

            const string sql = @"INSERT INTO Subjects (SubjectName, IsActive)VALUES (@name, 1);SELECT SCOPE_IDENTITY();";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@name", subjectName.Trim());
            con.Open();

            var idObj = cmd.ExecuteScalar();
            return Convert.ToInt32(idObj);
        }

        public int DeleteSubject(int subjectId)
        {
            // If you have FK PricingRule(SubjectID) WITHOUT cascade, delete pricing first
            const string sql = @"DELETE FROM PricingRule WHERE SubjectID = @id;DELETE FROM Subjects WHERE SubjectID = @id;";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", subjectId);
            con.Open();

            return cmd.ExecuteNonQuery(); // total rows affected
        }

        public int SetSubjectActive(int subjectId, bool isActive)
        {
            const string sql = @"UPDATE Subjects SET IsActive = @active WHERE SubjectID = @id";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", subjectId);
            con.Open();

            return cmd.ExecuteNonQuery();
        }

        // ---- Pricing ----

        public PricingRuleRow? GetPricingForSubject(int subjectId)
        {
            const string sql = @"
        SELECT TOP 1 PricingRuleID, SubjectID, AdminID, HourlyRate, MinHours, MaxHours, MaxPointDiscountFROM PricingRuleWHERE SubjectID = @sid";

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@sid", subjectId);
            con.Open();

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new PricingRuleRow
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
            // Basic business sanity
            if (minHours <= 0 || maxHours <= 0 || maxHours < minHours) throw new ArgumentException("Invalid hour range.");
            if (hourlyRate < 0) throw new ArgumentException("Hourly rate must be >= 0.");
            if (maxPointDiscount < 0) throw new ArgumentException("Max points discount must be >= 0.");

            // Upsert pattern: update if exists, else insert
            const string sql = @"
            IF EXISTS (SELECT 1 FROM PricingRule WHERE SubjectID = @sid)
            BEGIN
                UPDATE PricingRule
                SET AdminID = @aid,HourlyRate = @hr,MinHours = @minh,MaxHours = @maxh,MaxPointDiscount = @mpdWHERE SubjectID = @sid;
            END
            ELSE
            BEGIN
                INSERT INTO PricingRule (SubjectID, AdminID, HourlyRate, MinHours, MaxHours, MaxPointDiscount)
                VALUES (@sid, @aid, @hr, @minh, @maxh, @mpd);
            END";

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



        //============================== Leaderboard ==============================



        //============================== Analytics ==============================



        //============================== Admin Account ==============================


    }
}
