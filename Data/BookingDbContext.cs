using Microsoft.EntityFrameworkCore;
using MeetingRoomBookingSystem.Models;

namespace MeetingRoomBookingSystem.Data;

    public class BookingDbContext : DbContext
    {
        public BookingDbContext(DbContextOptions<BookingDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Room> Rooms { get; set; }

        public DbSet<BookingRecord> BookingRecords { get; set; }


    }
