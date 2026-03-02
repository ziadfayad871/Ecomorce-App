using DataAccess.Data;
using DataAccess.Models.Entities;
using DataAccess.Security;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Services
{
    public class MemberAuthService
    {
        private readonly ApplicationDbContext _db;

        public MemberAuthService(ApplicationDbContext db) => _db = db;

        public async Task<int> RegisterAsync(string fullName, string email, string password)
        {
            email = email.Trim().ToLower();
            var exists = await _db.Members.AnyAsync(m => m.Email == email);
            if (exists) throw new Exception("Email already exists");

            var member = new Member
            {
                FullName = fullName,
                Email = email,
                PasswordHash = ManualHasher.Hash(password),
                IsActive = true
            };

            _db.Members.Add(member);
            await _db.SaveChangesAsync();
            return member.Id;
        }

        public async Task<Member?> LoginAsync(string email, string password)
        {
            email = email.Trim().ToLower();
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email && m.IsActive);
            if (member == null) return null;

            return ManualHasher.Verify(password, member.PasswordHash) ? member : null;
        }
    }
}
