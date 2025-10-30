#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using J_Tutors_Web_Platform.Models.Scheduling;
using J_Tutors_Web_Platform.Models.Shared;

namespace J_Tutors_Web_Platform.Services
{
    /// <summary>
    /// Minimal Agenda service for Part 1:
    /// - GetAgendaCountsAsync()
    /// - GetAvailabilityBlocksAsync(...)
    /// </summary>
    public sealed class AdminAgendaService
    {
        private readonly string _connStr;

        // Preferred DI constructor (uses "AzureSql", then falls back to "DefaultConnection")
        public AdminAgendaService(IConfiguration cfg)
        {
            _connStr =
                cfg.GetConnectionString("AzureSql")
                ?? cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentException(
                    "Missing DB connection string: 'AzureSql' or 'DefaultConnection'.");
        }

        // Optional alternate constructor if you want to pass the string directly in tests
        public AdminAgendaService(string connectionString)
        {
            _connStr = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Returns header counts for Scheduled, Accepted, Paid, Cancelled.
        /// Robustly parses Status whether stored as int or enum name string.
        /// </summary>
        public async Task<(int scheduled, int accepted, int paid, int cancelled)> GetAgendaCountsAsync()
        {
            const string SQL = @"SELECT Status FROM dbo.TutoringSession;";

            int scheduled = 0, accepted = 0, paid = 0, cancelled = 0;

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(SQL, conn);
            await conn.OpenAsync();

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var statusObj = r["Status"];

                TutoringSessionStatus status;
                try
                {
                    if (statusObj is int i)
                    {
                        status = (TutoringSessionStatus)i;
                    }
                    else
                    {
                        var s = Convert.ToString(statusObj, CultureInfo.InvariantCulture)?.Trim();
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asInt))
                            status = (TutoringSessionStatus)asInt;
                        else if (Enum.TryParse<TutoringSessionStatus>(s, true, out var asEnum))
                            status = asEnum;
                        else
                            status = TutoringSessionStatus.Scheduled; // safe default
                    }
                }
                catch
                {
                    status = TutoringSessionStatus.Scheduled;
                }

                switch (status)
                {
                    case TutoringSessionStatus.Scheduled: scheduled++; break;
                    case TutoringSessionStatus.Accepted: accepted++; break;
                    case TutoringSessionStatus.Paid: paid++; break;
                    case TutoringSessionStatus.Cancelled: cancelled++; break;
                    default: break; // ignore Denied/Completed in the header for Part 1
                }
            }

            return (scheduled, accepted, paid, cancelled);
        }

        /// <summary>
        /// Returns availability blocks, optionally filtered by date window and/or admin.
        /// </summary>
        public async Task<IReadOnlyList<AvailabilityBlock>> GetAvailabilityBlocksAsync(
            DateTime? fromInclusive = null,
            DateTime? toExclusive = null,
            int? adminId = null)
        {
            var sql = @"SELECT AvailabilityBlockID, AdminID, BlockDate, StartTime, EndTime FROM dbo.AvailabilityBlock WHERE 1=1";
            if (fromInclusive.HasValue) sql += " AND CAST(BlockDate AS date) >= @from";
            if (toExclusive.HasValue) sql += " AND CAST(BlockDate AS date) <  @to";
            if (adminId.HasValue) sql += " AND AdminID = @a";
            sql += " ORDER BY BlockDate, StartTime";

            var list = new List<AvailabilityBlock>();

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(sql, conn);
            if (fromInclusive.HasValue) cmd.Parameters.Add("@from", SqlDbType.Date).Value = fromInclusive.Value.Date;
            if (toExclusive.HasValue) cmd.Parameters.Add("@to", SqlDbType.Date).Value = toExclusive.Value.Date;
            if (adminId.HasValue) cmd.Parameters.Add("@a", SqlDbType.Int).Value = adminId.Value;

            await conn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new AvailabilityBlock
                {
                    AvailabilityBlockID = Convert.ToInt32(r["AvailabilityBlockID"]),
                    AdminID = Convert.ToInt32(r["AdminID"]),
                    BlockDate = Convert.ToDateTime(r["BlockDate"]).Date,
                    StartTime = (TimeSpan)r["StartTime"],
                    EndTime = (TimeSpan)r["EndTime"]
                });
            }

            return list;
        }

        public async Task<int> CreateAvailabilityBlockAsync(int adminId, DateTime date, TimeSpan start, int durationMinutes)
        {
            if (durationMinutes <= 0)
                throw new ArgumentException("Duration must be positive.");

            var end = start.Add(TimeSpan.FromMinutes(durationMinutes));

            const string SQL = @"INSERT INTO dbo.AvailabilityBlock (AdminID, BlockDate, StartTime, EndTime) VALUES (@a, @d, @st, @et); SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(SQL, conn);
            cmd.Parameters.Add("@a", SqlDbType.Int).Value = adminId;
            cmd.Parameters.Add("@d", SqlDbType.Date).Value = date.Date;
            cmd.Parameters.Add("@st", SqlDbType.Time).Value = start;
            cmd.Parameters.Add("@et", SqlDbType.Time).Value = end;

            await conn.OpenAsync();
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return id;
        }

        public async Task<int> DeleteAvailabilityBlockAsync(int id)
        {
            const string SQL = "DELETE FROM dbo.AvailabilityBlock WHERE AvailabilityBlockID = @id;";
            await using var conn = new SqlConnection(_connStr);
            await using var cmd = new SqlCommand(SQL, conn);
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

    }
}
