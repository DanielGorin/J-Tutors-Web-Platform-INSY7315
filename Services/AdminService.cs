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



        //============================== Leaderboard ==============================



        //============================== Analytics ==============================



        //============================== Admin Account ==============================


    }
}
