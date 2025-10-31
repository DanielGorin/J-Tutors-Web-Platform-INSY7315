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
    public class AnalyticsService
    {
        private readonly string _connectionString;
        public AnalyticsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public int CalculateGlobalPoints()
        {
            int globalPoints;

            const string sql = "select SUM(Amount) from PointsReceipt where Type = @Type";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Type", PointsReceiptType.Earned);

            constring.Open();
            globalPoints = (int)cmd.ExecuteScalar();
            constring.Close();

            return globalPoints;
        }

        public int GetTotalSessions()
        {
            int totalSessions;

            const string sql = "select isnull(count(*), 0) from TutoringSession where Status = 'Paid' and Status = 'Accepted'";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();
            totalSessions = (int)cmd.ExecuteScalar();
            constring.Close();

            return totalSessions;
        }
        public int GetTotalMissed()
        {
            int totalSessions;

            const string sql = "select isnull(count(*), 0) from TutoringSession where Status = 'Cancelled'";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();
            totalSessions = (int)cmd.ExecuteScalar();
            constring.Close();

            return totalSessions;
        }

        public double CalculateAverageAttendance()
        {
            double divided = GetTotalSessions() - GetTotalMissed();
            double averageAttendance = (divided / GetTotalSessions()) * 100;

            return averageAttendance;
        }

        public double CalculateAmountUnpaid()
        {
            double amountUnpaid;

            const string sql = "select isnull(SUM(BaseCost), 0) from TutoringSession where Status = @Status";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@Status", "Accepted");

            constring.Open();
            amountUnpaid = Convert.ToDouble(cmd.ExecuteScalar());
            constring.Close();

            return amountUnpaid;
        }

        public double MonthlyRevenue() 
        {
            double paid;

            const string sql = "select isnull(SUM(BaseCost), 0) from TutoringSession where Status = 'Paid' and Month(SessionDate) = Month(GetDate()) and Year(SessionDate) = Year(GetDate())";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();
            paid = Convert.ToDouble(cmd.ExecuteScalar());
            constring.Close();

            return paid;
        }
    }
}
