namespace BaseLibrary.Entities
{
    public class SanctionType : BaseEntity
    {
        // Many to one relationship with Vacation
        public List<Sanction>? Sanctions { get; set; }
    }
}
