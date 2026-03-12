
namespace BaseLibrary.Entities
{
    public class UserRole
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public SystemRole Role { get; set; } = null!;
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;


    }
}
