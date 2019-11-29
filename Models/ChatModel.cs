using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Subtext.Models {
	[Serializable]
	public enum UserPresence {
		Offline,
		Online,
		Away,
		Busy
	}
	
	[Serializable]
	public class User {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		// Deleted users MUST have "!deleted_YYYYMMDD" appended to their username
		public string Name { get; set; }
		
		public byte[] Secret { get; set; }
		public byte[] Salt { get; set; }
		
		public UserPresence Presence { get; set; }
		public DateTime LastActive { get; set; }
		
		public string Status { get; set; }
		
		public bool IsLocked { get; set; }
		public string LockReason { get; set; }
		public DateTime LockExpiry { get; set; }
		
		public bool IsDeleted { get; set; }
		
		[InverseProperty("Owner")]
		public List<BlockRecord> Blocked { get; set; }
		
		[InverseProperty("Owner")]
		public List<FriendRecord> Friends { get; set; }
		
		[InverseProperty("Recipient")]
		public List<FriendRequest> FriendRequests { get; set; }
		
		[InverseProperty("Owner")]
		public List<PublicKey> Keys { get; set; }
	}
	
	[Serializable]
	public class BlockRecord {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? OwnerId { get; set; }
		[ForeignKey("OwnerId")]
		public User Owner { get; set; }
		
		public Guid? BlockedId { get; set; }
		[ForeignKey("BlockedId")]
		public User Blocked { get; set; }
	}
	
	[Serializable]
	public class FriendRecord {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? OwnerId { get; set; }
		[ForeignKey("OwnerId")]
		public User Owner { get; set; }
		
		public Guid? FriendId { get; set; }
		[ForeignKey("FriendId")]
		public User Friend { get; set; }
	}
	
	[Serializable]
	public class FriendRequest {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? SenderId { get; set; }
		[ForeignKey("SenderId")]
		public User Sender { get; set; }
		
		public Guid? RecipientId { get; set; }
		[ForeignKey("RecipientId")]
		public User Recipient { get; set; }
	}
	
	[Serializable]
	public class PublicKey {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? OwnerId { get; set; }
		[ForeignKey("OwnerId")]
		public User Owner { get; set; }
		
		public byte[] KeyData { get; set; }
		
		public DateTime PublishTime { get; set; }
	}
	
	[Serializable]
	public enum BoardEncryption {
		None,
		SharedKey,
		GnuPG
	}
	
	[Serializable]
	public class Board {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		// DM boards MUST be named "!direct_ID"
		public string Name { get; set; }
		
		public DateTime LastUpdate { get; set; }
		public DateTime LastSignificantUpdate { get; set; }
		
		public Guid? OwnerId { get; set; }
		[ForeignKey("OwnerId")]
		public User Owner { get; set; }
		
		public BoardEncryption Encryption { get; set; }
		
		[InverseProperty("Board")]
		public List<Message> Messages { get; set; }
		
		[InverseProperty("Board")]
		public List<MemberRecord> Members { get; set; }
	}
	
	[Serializable]
	public class Message {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? BoardId { get; set; }
		[ForeignKey("BoardId")]
		public Board Board { get; set; }
		
		public Guid? AuthorId { get; set; }
		[ForeignKey("AuthorId")]
		public User Author { get; set; }
		
		public DateTime Timestamp { get; set; }
		
		public string Type { get; set; }
		public byte[] Content { get; set; }
		public bool IsSystem { get; set; }
	}
	
	[Serializable]
	public class MemberRecord {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? BoardId { get; set; }
		[ForeignKey("BoardId")]
		public Board Board { get; set; }
		
		public Guid? UserId { get; set; }
		[ForeignKey("UserId")]
		public User User { get; set; }
	}
	
	[Serializable]
	public class Admin {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public byte[] Secret { get; set; }
		
		public DateTime LastAction { get; set; }
		
		public bool IsLoggedIn { get; set; }
		public byte[] Challenge { get; set; }
		
		[InverseProperty("Admin")]
		public List<PermissionRecord> Permissions { get; set; }
	}
	
	[Serializable]
	public class AuditLogEntry {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? AdminId { get; set; }
		[ForeignKey("AdminId")]
		public Admin Admin { get; set; }
		
		public DateTime Timestamp { get; set; }
		
		public string Action { get; set; }
		public string Details { get; set; }
	}
	
	[Serializable]
	public class PermissionRecord {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? AdminId { get; set; }
		[ForeignKey("AdminId")]
		public Admin Admin { get; set; }
		
		public string Action { get; set; }
	}
	
	[Serializable]
	public class Session {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? UserId { get; set; }
		[ForeignKey("UserId")]
		public User User { get; set; }
		
		public DateTime Timestamp { get; set; }
	}
	
	[Serializable]
	public class AdminSession {
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }
		
		public Guid? AdminId { get; set; }
		[ForeignKey("AdminId")]
		public Admin Admin { get; set; }
		
		public DateTime Timestamp { get; set; }
	}
	
	public class ChatContext : DbContext {
		
		static bool createdCheck = false;
		
		public ChatContext(DbContextOptions<ChatContext> options) : base(options) {
			if (!createdCheck) {
				Database.EnsureCreated();
				// TODO migration???
				createdCheck = true;
			}
		}
		
		public DbSet<User> Users { get; set; }
		public DbSet<BlockRecord> BlockRecords { get; set; }
		public DbSet<FriendRecord> FriendRecords { get; set; }
		public DbSet<FriendRequest> FriendRequests { get; set; }
		public DbSet<PublicKey> PublicKeys { get; set; }
		public DbSet<Board> Boards { get; set; }
		public DbSet<Message> Messages { get; set; }
		public DbSet<MemberRecord> MemberRecords { get; set; }
		public DbSet<Admin> Admins { get; set; }
		public DbSet<AuditLogEntry> AuditLog { get; set; }
		public DbSet<PermissionRecord> PermissionRecords { get; set; }
		public DbSet<Session> Sessions { get; set; }
		public DbSet<AdminSession> AdminSessions { get; set; }
		
		protected override void OnModelCreating(ModelBuilder builder) {
			builder.Entity<User>()
				.HasIndex(u => u.Name)
				.IsUnique();
			
			builder.Entity<BlockRecord>()
				.HasIndex(br => br.OwnerId);
			builder.Entity<BlockRecord>()
				.HasOne(br => br.Owner)
				.WithMany(u => u.Blocked)
				.HasForeignKey(br => br.OwnerId);
			builder.Entity<BlockRecord>()
				.HasOne(br => br.Blocked)
				.WithMany()
				.HasForeignKey(br => br.BlockedId);
			
			builder.Entity<FriendRecord>()
				.HasIndex(fr => fr.OwnerId);
			builder.Entity<FriendRecord>()
				.HasOne(fr => fr.Owner)
				.WithMany(u => u.Friends)
				.HasForeignKey(fr => fr.OwnerId);
			builder.Entity<FriendRecord>()
				.HasOne(fr => fr.Friend)
				.WithMany()
				.HasForeignKey(fr => fr.FriendId);
			
			builder.Entity<FriendRequest>()
				.HasIndex(fr => fr.RecipientId);
			builder.Entity<FriendRequest>()
				.HasOne(fr => fr.Recipient)
				.WithMany(u => u.FriendRequests)
				.HasForeignKey(fr => fr.RecipientId);
			builder.Entity<FriendRequest>()
				.HasOne(fr => fr.Sender)
				.WithMany()
				.HasForeignKey(fr => fr.SenderId);
			
			builder.Entity<PublicKey>()
				.HasIndex(pk => pk.OwnerId);
			builder.Entity<PublicKey>()
				.HasOne(pk => pk.Owner)
				.WithMany(u => u.Keys)
				.HasForeignKey(pk => pk.OwnerId);
			
			builder.Entity<Board>()
				.HasIndex(b => b.Name)
				.IsUnique();
			
			builder.Entity<Message>()
				.HasIndex(m => m.BoardId);
			builder.Entity<Message>()
				.HasOne(m => m.Board)
				.WithMany(b => b.Messages)
				.HasForeignKey(m => m.BoardId);
			builder.Entity<Message>()
				.HasOne(m => m.Author)
				.WithMany()
				.HasForeignKey(m => m.AuthorId);
			
			builder.Entity<MemberRecord>()
				.HasIndex(mr => mr.BoardId);
			builder.Entity<MemberRecord>()
				.HasOne(mr => mr.Board)
				.WithMany(b => b.Members)
				.HasForeignKey(mr => mr.BoardId);
			builder.Entity<MemberRecord>()
				.HasOne(mr => mr.User)
				.WithMany()
				.HasForeignKey(mr => mr.UserId);
			
			builder.Entity<AuditLogEntry>()
				.HasIndex(ale => ale.AdminId);
			
			builder.Entity<PermissionRecord>()
				.HasIndex(pr => pr.AdminId);
			
			builder.Entity<Session>()
				.HasIndex(s => s.UserId);
			
			builder.Entity<AdminSession>()
				.HasIndex(s => s.AdminId);
		}
		
		public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			// TODO clean up stuff
			return await base.SaveChangesAsync();
		}
	}
}