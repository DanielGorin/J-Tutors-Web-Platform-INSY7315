/*
 * Developed By:
 * Fourloop (Daniel Gorin, William McPetrie, Moegammad-Yaseen Salie, Michael Amm)
 * For:
 * Varsity College INSY7315 WIL Project
 * Client:
 * J-Tutors
 * File Name:
 * AnalyticsService
 * File Purpose:
 * This is a service that handles methods for anyltic calculations for admins
 * AI Usage:
 * AI has been used at points throughout this project AI declaration available in the ReadMe
 */
using J_Tutors_Web_Platform.Models.Admins;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Models.Subjects;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using NuGet.Configuration;
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

        // ==================================================================================================
        //  PURPOSE:
        //          - getting all points earned from all users on the site
        // ==================================================================================================

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

        // ==================================================================================================
        //  PURPOSE:
        //          - getting total sessions from all users
        //          - getting total missed sessions from all users
        //          - using the above two methods to calculate average attendance
        // ==================================================================================================

        public int GetTotalSessions() // for use within CalculateAverageAttendance
        {
            int totalSessions;

            const string sql = "select isnull(count(*), 0) from TutoringSession where Status = 'Paid' and Status = 'Accepted'"; //pulling total number of sessions that were paid or accepted
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();
            totalSessions = (int)cmd.ExecuteScalar();
            constring.Close();

            return totalSessions;
        }
        public int GetTotalMissed() // for use within CalculateAverageAttendance
        {
            int totalSessions;

            const string sql = "select isnull(count(*), 0) from TutoringSession where Status = 'Cancelled'"; // pulling sessions that were cancelled
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();
            totalSessions = (int)cmd.ExecuteScalar();
            constring.Close();

            return totalSessions;
        }

        public double CalculateAverageAttendance()
        {
            // (part/whole)*100 = percentage
            double divided = GetTotalSessions() - GetTotalMissed();
            double averageAttendance = (divided / GetTotalSessions()) * 100;

            return averageAttendance;
        }

        // ==================================================================================================
        //  PURPOSE:
        //          - method that pulls all unpaid amounts from accepted sessions
        // ==================================================================================================

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

        // ==================================================================================================
        //  PURPOSE:
        //          - method for calculating total monthly revenue, basicly tallying up all paid sessions in the current month and yearr
        // ==================================================================================================

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
