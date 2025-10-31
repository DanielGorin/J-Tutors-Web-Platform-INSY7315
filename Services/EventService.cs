using J_Tutors_Web_Platform.Models.Admins;
using J_Tutors_Web_Platform.Models.Events;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Models.Subjects;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Bson;

namespace J_Tutors_Web_Platform.Services
{
    public class EventService
    {
        private readonly string _connectionString;
        public EventService(string connectionString)
        {
            _connectionString = connectionString;
        }

        //====================UNIVERSAL METHODS====================//
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
        //======================INNER METHODS======================//

        public int GetCurrentParticipants(int EventID)
        {
            int numParticipants;

            const string sql = "select count(*) from EventParticipation where EventID = @EventID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);

            constring.Open();

            var result = cmd.ExecuteScalar();

            if (result == DBNull.Value || result == null)
            {
                numParticipants = 0;
            }
            else
            {
                numParticipants = (int)result;
            }

            constring.Close();

            return numParticipants;
        }

        public bool IsUserParticipating(int EventID, int UserID)
        {
            bool isParticipating;
            const string sql = "select count(*) from EventParticipation where EventID = @EventID and UserID = @UserID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);
            cmd.Parameters.AddWithValue("@UserID", UserID);

            constring.Open();
            
            var result = (int)cmd.ExecuteScalar();

            if (result > 0) 
            {
                isParticipating = true;
            }
            else
            {
                isParticipating = false;
            }
            
            constring.Close();
            
            return isParticipating;
        }

        //======================EVENT METHODS======================//

        public List<DetailedEventRow> GetEvents() 
        {
            var eventsList = new List<DetailedEventRow>();
            int currentParticpants;        

            const string sql = "select * from Events";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                currentParticpants = GetCurrentParticipants(reader.GetInt32(0));

                eventsList.Add(new DetailedEventRow
                {
                    EventID = reader.GetInt32(0),
                    AdminID = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ImageURL = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Location = reader.IsDBNull(5) ? null : reader.GetString(5),
                    EventDate = DateOnly.FromDateTime(reader.GetDateTime(6)),
                    StartTime = TimeOnly.FromTimeSpan(reader.GetTimeSpan(7)),
                    //StartTime = reader.GetTimeOnly(7),
                    DurationMinutes = reader.GetInt32(8),
                    PointsReward = reader.GetInt32(10),
                    GoalParticipants = reader.GetInt32(9),
                    WhatsappGroupUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
                    Status = Enum.TryParse<EventStatus>(reader.GetString(12), true, out var status) ? status : EventStatus.Draft,
                    CreationDate = DateOnly.FromDateTime(reader.GetDateTime(13)),
                    UpdateDate = DateOnly.FromDateTime(reader.GetDateTime(14)),
                    CurrentParticipants = currentParticpants
                });
            }

            return eventsList;
        }

        public List<DetailedEventRow> GetUserEvents(string Username)
        {
            var eventsList = new List<DetailedEventRow>();
            int currentParticpants;
            bool isUserParticipating;

            const string sql = "select * from Events";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                isUserParticipating = IsUserParticipating(reader.GetInt32(0), GetUserID(Username));
                currentParticpants = GetCurrentParticipants(reader.GetInt32(0));

                if (!isUserParticipating) 
                {
                    eventsList.Add(new DetailedEventRow
                    {
                        EventID = reader.GetInt32(0),
                        AdminID = reader.GetInt32(1),
                        Title = reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ImageURL = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Location = reader.IsDBNull(5) ? null : reader.GetString(5),
                        EventDate = DateOnly.FromDateTime(reader.GetDateTime(6)),
                        StartTime = TimeOnly.FromTimeSpan(reader.GetTimeSpan(7)),
                        //StartTime = reader.GetTimeOnly(7),
                        DurationMinutes = reader.GetInt32(8),
                        PointsReward = reader.GetInt32(10),
                        GoalParticipants = reader.GetInt32(9),
                        WhatsappGroupUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
                        Status = Enum.TryParse<EventStatus>(reader.GetString(12), true, out var status) ? status : EventStatus.Draft,
                        CreationDate = DateOnly.FromDateTime(reader.GetDateTime(13)),
                        UpdateDate = DateOnly.FromDateTime(reader.GetDateTime(14)),
                        CurrentParticipants = currentParticpants
                    });
                }
            }

            return eventsList;
        }

        public List<DetailedEventRow> GetUserEventHistory(string Username)
        {
            var eventsList = new List<DetailedEventRow>();
            int currentParticpants;
            bool isUserParticipating;

            const string sql = "select * from Events";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                isUserParticipating = IsUserParticipating(reader.GetInt32(0), GetUserID(Username));
                currentParticpants = GetCurrentParticipants(reader.GetInt32(0));

                if (isUserParticipating)
                {
                    eventsList.Add(new DetailedEventRow
                    {
                        EventID = reader.GetInt32(0),
                        AdminID = reader.GetInt32(1),
                        Title = reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ImageURL = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Location = reader.IsDBNull(5) ? null : reader.GetString(5),
                        EventDate = DateOnly.FromDateTime(reader.GetDateTime(6)),
                        StartTime = TimeOnly.FromTimeSpan(reader.GetTimeSpan(7)),
                        //StartTime = reader.GetTimeOnly(7),
                        DurationMinutes = reader.GetInt32(8),
                        PointsReward = reader.GetInt32(10),
                        GoalParticipants = reader.GetInt32(9),
                        WhatsappGroupUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
                        Status = Enum.TryParse<EventStatus>(reader.GetString(12), true, out var status) ? status : EventStatus.Draft,
                        CreationDate = DateOnly.FromDateTime(reader.GetDateTime(13)),
                        UpdateDate = DateOnly.FromDateTime(reader.GetDateTime(14)),
                        CurrentParticipants = currentParticpants
                    });
                }
            }

            return eventsList;
        }

        public List<Event> GetEventDetails(int EventID, string Username) 
        {
            var eventsList = new List<Event>();
            int currentParticpants;
            int eventID;


            const string sql = "select * from Events";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                currentParticpants = GetCurrentParticipants(reader.GetInt32(0));
                eventID = reader.GetInt32(0);

                if (EventID == eventID)
                {
                    eventsList.Add(new Event
                    {
                        EventID = eventID,
                        AdminID = reader.GetInt32(1),
                        Title = reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ImageURL = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Location = reader.IsDBNull(5) ? null : reader.GetString(5),
                        EventDate = DateOnly.FromDateTime(reader.GetDateTime(6)),
                        StartTime = reader.GetTimeSpan(7),
                        DurationMinutes = reader.GetInt32(8),
                        PointsReward = reader.GetInt32(10),
                        GoalParticipants = reader.GetInt32(9),
                        WhatsappGroupUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
                        Status = Enum.TryParse<EventStatus>(reader.GetString(12), true, out var status) ? status : EventStatus.Draft,
                        CreationDate = DateOnly.FromDateTime(reader.GetDateTime(13)),
                    });
                }
            }

            return eventsList;
        }

        public void CreateEvent(int AdminID, string Title, string description, string ImageURL, string Location, DateOnly EventDate, TimeOnly StartTime, int DurationMinutes, int PointsReward, int GoalParticipants, string WhatsappGroupURL, string Status, DateOnly creationDate, DateOnly updateDate)
        {
            Console.WriteLine("reached start of CreateEvent in service");

            const string sql = "insert into Events (AdminID, Title, Description, ImageURL, Location, EventDate, StartTime, DurationMinutes, PointsReward, GoalParticipants, WhatsappGroupURL, Status, CreationDate, UpdateDate) " +
                               "values (@AdminID, @Title, @Description, @ImageURL, @Location, @EventDate, @StartTime, @DurationMinutes, @PointsReward, @GoalParticipants, @WhatsappGroupURL, @Status, @CreationDate, @UpdateDate)";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@AdminID", AdminID);
            cmd.Parameters.AddWithValue("@Title", Title);
            cmd.Parameters.AddWithValue("@Description", description);
            cmd.Parameters.AddWithValue("@ImageURL", ImageURL);
            cmd.Parameters.AddWithValue("@Location", Location);
            cmd.Parameters.AddWithValue("@EventDate", EventDate);
            cmd.Parameters.AddWithValue("@StartTime", StartTime);
            cmd.Parameters.AddWithValue("@DurationMinutes", DurationMinutes);
            cmd.Parameters.AddWithValue("@PointsReward", PointsReward);
            cmd.Parameters.AddWithValue("@GoalParticipants", GoalParticipants);
            cmd.Parameters.AddWithValue("@WhatsappGroupURL", WhatsappGroupURL);
            cmd.Parameters.AddWithValue("@Status", Status);
            cmd.Parameters.AddWithValue("@CreationDate", creationDate);
            cmd.Parameters.AddWithValue("@UpdateDate", updateDate);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();

            Console.WriteLine("reached end of CreateEvent in service");
        }

        public void UpdateEvent(int EventID, int AdminID, string Title, string Description, string ImageURL, string Location, DateOnly EventDate, TimeOnly StartTime, int DurationMinutes, int PointsReward, int GoalParticipants, string WhatsappGroupURL, string Status, DateOnly updateDate)
        {
            const string sql = "update Events set Title = @Title, Description = @Description, ImageURL = @ImageURL, Location = @Location, EventDate = @EventDate, StartTime = @StartTime, DurationMinutes = @DurationMinutes, PointsReward = @PointsReward, GoalParticipants = @GoalParticipants, WhatsappGroupURL = @WhatsappGroupURL, Status = @Status, UpdateDate = @UpdateDate " +
                               "where EventID = @EventID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);
            cmd.Parameters.AddWithValue("@EventID", EventID);
            cmd.Parameters.AddWithValue("@Title", Title);
            cmd.Parameters.AddWithValue("@Description", Description);
            cmd.Parameters.AddWithValue("@ImageURL", ImageURL);
            cmd.Parameters.AddWithValue("@Location", Location);
            cmd.Parameters.AddWithValue("@EventDate", EventDate);
            cmd.Parameters.AddWithValue("@StartTime", StartTime);
            cmd.Parameters.AddWithValue("@DurationMinutes", DurationMinutes);
            cmd.Parameters.AddWithValue("@PointsReward", PointsReward);
            cmd.Parameters.AddWithValue("@GoalParticipants", GoalParticipants);
            cmd.Parameters.AddWithValue("@WhatsappGroupURL", WhatsappGroupURL);
            cmd.Parameters.AddWithValue("@Status", Status);
            cmd.Parameters.AddWithValue("@UpdateDate", updateDate);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();

            Console.WriteLine("reached end of Update in service");
        }

        public void JoinEvent(int EventID, string username) 
        {
            int userID = GetUserID(username);

            Console.WriteLine("inside join event with " + EventID);

            const string sql = "insert into EventParticipation (EventID, UserID, JoinDate, Attendance) " +
                               "values (@EventID, @UserID, @JoinDate, @Attendance)";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);
            cmd.Parameters.AddWithValue("@UserID", userID);
            cmd.Parameters.AddWithValue("@JoinDate", DateOnly.FromDateTime(DateTime.Now));
            cmd.Parameters.AddWithValue("@Attendance", 0);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();

            Console.WriteLine("reached end of join event");
        }


    }
}
