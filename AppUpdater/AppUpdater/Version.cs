namespace AppUpdater
{
    /// <summary>
    /// Version control handling from 1 to higher values than 4
    /// </summary>
    public class Version : System.IComparable<Version>
    {
        #region Private Members
        /// <summary>
        /// Version as string
        /// </summary>
        private string m_VersionString;
        #endregion

        #region Public Properties
        /// <summary>
        /// Version numbers array
        /// </summary>
        public uint[] VersionNumbers { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a zero default version
        /// </summary>
        public Version()
        {
            m_VersionString = "0";
            VersionNumbers = new uint[] { 0 };
        }
        /// <summary>
        /// Creates a version control by using string format like "1", "1.0", "1.0.0", ... "1.0.0.0.0.0", etc.
        /// </summary>
        public Version(string Version)
        {
            // string
            m_VersionString = Version;
            // to compare
            var numbers = Version.Split('.');
            VersionNumbers = new uint[numbers.Length];
            for (int i = 0; i < VersionNumbers.Length; i++)
                VersionNumbers[i] = uint.Parse(numbers[i]);
        }
        /// <summary>
        /// Creates a version control by providing the control numbers from high to lower version positions
        /// </summary>
        public Version(uint[] VersionNumbers)
        {
            // string
            this.m_VersionString = string.Join(".", VersionNumbers);
            // to comparer
            this.VersionNumbers = new uint[VersionNumbers.Length];
            System.Array.Copy(VersionNumbers, this.VersionNumbers, VersionNumbers.Length);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Compare this version with another one
        /// </summary>
        public int CompareTo(Version other)
        {
            return Compare(this, other);
        }
        public override string ToString()
        {
            return m_VersionString;
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        #endregion

        #region Public Static Helpers
        /// <summary>
        /// Compare two versions, first vs second.
        /// </summary>
        public static int Compare(Version first,Version second)
        {
            if (first.VersionNumbers.Length == second.VersionNumbers.Length)
            {
                for (int i = 0; i < first.VersionNumbers.Length; i++)
                {
                    if (first.VersionNumbers[i] > second.VersionNumbers[i])
                        return 1;
                    else if (first.VersionNumbers[i] < second.VersionNumbers[i])
                        return -1;
                }
            }
            else
            {
                int minLength = second.VersionNumbers.Length > first.VersionNumbers.Length ? first.VersionNumbers.Length : second.VersionNumbers.Length;
                for (int i = 0; i < minLength; i++)
                {
                    if (first.VersionNumbers[i] > second.VersionNumbers[i])
                        return 1;
                    else if (first.VersionNumbers[i] < second.VersionNumbers[i])
                        return -1;
                }
                var higherVersionNumbers = first.VersionNumbers.Length > second.VersionNumbers.Length ? first.VersionNumbers : second.VersionNumbers;
                for (int i = minLength; i < higherVersionNumbers.Length; i++)
                {
                    if(higherVersionNumbers[i] > 0)
                    {
                        if (higherVersionNumbers == first.VersionNumbers)
                            return 1;
                        else
                            return -1;
                    }
                }
            }
            return 0;
        }
        #endregion

        #region Basic Operators
        public static bool operator <(Version first, Version second)
        {
            return Compare(first, second) < 0;
        }
        public static bool operator >(Version first, Version second)
        {
            return Compare(first, second) > 0;
        }
        public static bool operator ==(Version first, Version second)
        {
            return Compare(first,second) == 0;
        }
        public static bool operator !=(Version first, Version second)
        {
            return Compare(first, second) != 0;
        }
        #endregion
    }
}
