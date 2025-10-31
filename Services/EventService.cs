using J_Tutors_Web_Platform.Models.Admins;
using J_Tutors_Web_Platform.Models.Events;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;
using J_Tutors_Web_Platform.Models.Subjects;
using J_Tutors_Web_Platform.Models.Users;
using J_Tutors_Web_Platform.ViewModels;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using Newtonsoft.Json.Bson;
using System.Data;
using System.Security.Claims;

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

        public string GetUsername(int UserID)
        {
            Console.WriteLine("inside get username with " + UserID);

            const string sql = "select Username from Users where UserID = @UserID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", UserID);

            constring.Open();

            string username = (string)cmd.ExecuteScalar();

            constring.Close();

            Console.WriteLine("retrieved username: " + username);
            return username;
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
            const string sql = "select count(*) from EventParticipation where EventID = @EventID and UserID = @UserID"; //getting count of 
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

        public List<UserParticipationRow> GetEventUsers(int EventID) 
        {
            var UserParticipationRow = new List<UserParticipationRow>();
            string username;

            const string sql = "select * from EventParticipation where EventID = @EventID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);

            constring.Open();

            using SqlDataReader reader = cmd.ExecuteReader(); // 

            while (reader.Read())
            {
                username = GetUsername(reader.GetInt32(2));

                UserParticipationRow.Add(new UserParticipationRow
                {
                    EventID = reader.GetInt32(1),
                    UserID = reader.GetInt32(2),
                    Username = username,
                    JoinDate = DateOnly.FromDateTime(reader.GetDateTime(3)),
                    Attended = reader.GetBoolean(4)
                });
            }

            return UserParticipationRow;
        }

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

                eventsList.Add(new DetailedEventRow // filling list with event data from database
                {
                    EventID = reader.GetInt32(0),
                    AdminID = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ImageURL = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Location = reader.IsDBNull(5) ? null : reader.GetString(5),
                    EventDate = DateOnly.FromDateTime(reader.GetDateTime(6)),
                    StartTime = TimeOnly.FromTimeSpan(reader.GetTimeSpan(7)),
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

                if (!isUserParticipating) //filling list with only "new" / unjoined events, so that  it is not displayed on new event page
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

                if (isUserParticipating) // checks if user is participating in event,, and only fills list if they are, allows tracking on page that this populates, so that the user sees what events they have done/ have signed up for 
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
            //updateing entire event in case admin changes a detail, all this is populated in AEventDetails
            const string sql = "update Events set Title = @Title, Description = @Description, ImageURL = @ImageURL, Location = @Location, EventDate = @EventDate, StartTime = @StartTime, DurationMinutes = @DurationMinutes, PointsReward = @PointsReward, GoalParticipants = @GoalParticipants, WhatsappGroupURL = @WhatsappGroupURL, Status = @Status, UpdateDate = @UpdateDate where EventID = @EventID";
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

            //Console.WriteLine("reached end of Update in service"); // console write lines for debugging, you may see a bunch of them, they can be removed in actual deployment however they are nice to uncomment if there are issues in the future
        }

        public void JoinEvent(int EventID, string username) 
        {
            int userID = GetUserID(username);

            //Console.WriteLine("inside join event with " + EventID);

            const string sql = "insert into EventParticipation (EventID, UserID, JoinDate, Attendance) values (@EventID, @UserID, @JoinDate, @Attendance)";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);
            cmd.Parameters.AddWithValue("@UserID", userID);
            cmd.Parameters.AddWithValue("@JoinDate", DateOnly.FromDateTime(DateTime.Now));
            cmd.Parameters.AddWithValue("@Attendance", 0);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();

            //Console.WriteLine("reached end of join event");
        }

        public void DeleteUserFromEvent(int EventID, int UserID) 
        {
            const string sql = "delete from EventParticipation where EventID = @EventID and UserID = @UserID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);
            cmd.Parameters.AddWithValue("@UserID", UserID);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();
        }

        public int GetPointsReward(int EventID) 
        {
            //Console.WriteLine("inside get participation id with EventID: " + EventID);

            const string sql = "select PointsReward from Events where EventID = @EventID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);

            constring.Open();
            int points = (int)cmd.ExecuteScalar();
            constring.Close();

            return points;
        }

        public int GetAdminIDFromEvent(int EventID)
        {
            Console.WriteLine("inside get participation id with EventID: " + EventID);

            const string sql = "select AdminID from Events where EventID = @EventID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);

            constring.Open();
            int id = (int)cmd.ExecuteScalar();
            constring.Close();

            return id;
        }

        public int GetParticipationID(int EventID, int UserID)
        {
            Console.WriteLine("inside get participation id with EventID: " + EventID + " and UserID: " + UserID);

            const string sql = "select EventParticipationID from EventParticipation where EventID = @EventID and UserID = @UserID";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@EventID", EventID);
            cmd.Parameters.AddWithValue("@UserID", UserID);

            constring.Open();
            var result = cmd.ExecuteScalar();
            constring.Close();

            if (result == null)
            {
                return 0;
            }
            else if (result == DBNull.Value)
            {
                return 0;
            }
            else
            {
                int id = (int)result;
                return id;
            }
        }

        public void GenerateReceiptFromEvent(int EventID, int UserID) //broken up into lots of individual methods to keep it smaller and simpler
        {
            int amount = GetPointsReward(EventID);
            int adminID = GetAdminIDFromEvent(EventID);
            int eventParticipationID = GetParticipationID(EventID, UserID);
            
            bool affectsAllTime = true;

            DateTime receiptDate = DateTime.Now;

            PointsReceiptType pointsReceiptType = PointsReceiptType.Earned;

            string reason = "Participation in event ID " + EventID;
            string reference = "Event ID " + EventID + ", Admin ID " + adminID;
            string notes = "Auto-generated receipt for your(" + UserID + ") event(" + EventID + ") participation, for the amount of " + amount + " points.";

            /////================ Insert into PointsReceipt ===================///

            const string sql = "insert into PointsReceipt(UserID, EventParticipationID, AdminID, ReceiptDate, Type, Amount, Reason, Reference, AffectsAllTime, Notes) values(@UserID, @EventParticipationID, @AdminID, @ReceiptDate, @Type, @Amount, @Reason, @Reference, @AffectsAllTime, @Notes)";
            using var constring = new SqlConnection(_connectionString); //using connection string to connect to database, using ensures connection is closed after use
            using var cmd = new SqlCommand(sql, constring);

            cmd.Parameters.AddWithValue("@UserID", UserID);
            cmd.Parameters.AddWithValue("@EventParticipationID", eventParticipationID);
            cmd.Parameters.AddWithValue("@AdminID", adminID);
            cmd.Parameters.AddWithValue("@ReceiptDate", receiptDate);
            cmd.Parameters.AddWithValue("@Type", pointsReceiptType);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Reason", reason);
            cmd.Parameters.AddWithValue("@Reference", reference);
            cmd.Parameters.AddWithValue("@AffectsAllTime", SqlDbType.Bit).Value = affectsAllTime;
            cmd.Parameters.AddWithValue("@Notes", notes);

            constring.Open();
            cmd.ExecuteNonQuery();
            constring.Close();
        }


    }
}
