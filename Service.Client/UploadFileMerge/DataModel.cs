namespace Service.Client.UploadFileMerge
{
    public class DataModel
    {
        public string Type { get; set; } = null!;
        public string Message { get; set; } = null!;

        // 用于 HashSet 去重
        public override bool Equals(object? obj)
        {
            if (obj is not null && obj is DataModel other)
            {
                return Type == other.Type && Message == other.Message;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Message);
        }
    }

}
