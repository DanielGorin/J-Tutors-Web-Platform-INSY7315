using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using Microsoft.Data.SqlClient;

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
            var list = new List<TutoringSession>();

            const string sql = "select * from TutoringSessions";
            using var constring = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read()) 
            {
                list.Add(new TutoringSession
                {
                    TutoringSessionID = Convert.ToInt32(reader["TutoringSessionID"]),
                    UserID = Convert.ToInt32(reader["UserID"]),
                    AdminID = Convert.ToInt32(reader["AdminID"]),
                    SubjectID = Convert.ToInt32(reader["SubjectID"]),
                    SessionDate = Convert.ToDateTime(reader["SessionDate"]),
                    DurationHours = Convert.ToDecimal(reader["DurationHours"]),
                    BaseCost = Convert.ToDecimal(reader["BaseCost"]),
                    PointsSpent = Convert.ToInt32(reader["PointsSpent"]),
                    Status = (TutoringSessionStatus)Convert.ToInt32(reader["Status"]),
                    CancellationDate = reader["CancellationDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["CancellationDate"]),
                    PaidDate = reader["PaidDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(reader["PaidDate"])
                });
            }

            return list;
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
