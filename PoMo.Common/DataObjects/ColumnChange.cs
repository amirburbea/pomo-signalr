namespace PoMo.Common.DataObjects
{
    public sealed class ColumnChange
    {
        public int ColumnOrdinal
        {
            get;
            set;
        }

        public object Value
        {
            get;
            set;
        }
    }
}