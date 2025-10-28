using J_Tutors_Web_Platform.Models.Admins;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Models.Users;
using Microsoft.Data.SqlClient;
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

        //============================== DashBoard ==============================



        //============================== Sessions & Calender ==============================

        public List<TutoringSession> GetTutoringSessions()
        {
            var Sessionlist = new List<TutoringSession>();

            const string sql = "select * from TutoringSessions";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

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

            return Sessionlist;
        }

        public List<AvailabilityBlock> GetAvailabilityBlocks()
        {
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

        //public string FilterCalender() 
        //{
        //    return "";
        //}

        //public string FilterBookings() 
        //{
        //    return "";
        //}

        //public string AcceptTutorSession()
        //{
        //    return "";
        //}

        //============================== Users ==============================

        //public string GetAllUsers()
        //{
        //    return "";
        //}

        //public string SortByActivity()
        //{
        //    return "";
        //}

        //public string SortByCurrentPoints()
        //{
        //    return "";
        //}

        //public string SortByAllPoints()
        //{
        //    return "";
        //}

        //public string UnpaidAmount()
        //{
        //    return "";
        //}

        //public string Name()
        //{
        //    return "";
        //}

        //============================== Events ==============================



        //============================== Files ==============================



        //============================== Quotations ==============================



        //============================== Leaderboard ==============================



        //============================== Analytics ==============================



        //============================== Account ==============================



    }
}
