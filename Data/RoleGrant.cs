namespace RoleyPoley.Data
{
    public class RoleGrant
    {
        /// <summary>[role] can give these roles to any user</summary>
        public Dictionary<ulong, List<ulong>> Grants { get; set; }
    }
}
