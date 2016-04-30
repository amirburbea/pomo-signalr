﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using PoMo.Client.DataBoundObjects;
using PoMo.Common;
using PoMo.Common.DataObjects;

namespace PoMo.Client.Views
{
    public abstract class SubscriberViewModelBase : ViewModelBase
    {
        private DataBoundObjectCollection _data;
        private DataTable _dataTable;
        private bool _isActive;
        private decimal _pnl;

        protected SubscriberViewModelBase(Dispatcher dispatcher, IConnectionManager connectionManager)
            : base(dispatcher, connectionManager)
        {
        }

        public DataBoundObjectCollection Data
        {
            get
            {
                return this._data;
            }
            private set
            {
                this.SetValue(ref this._data, value);
            }
        }

        public bool IsActive
        {
            get
            {
                return this._isActive;
            }
            set
            {
                if (this._isActive == value)
                {
                    return;
                }
                this._isActive = value;
                if (this.ConnectionManager.ConnectionStatus != ConnectionStatus.Connected)
                {
                    return;
                }
                if (value)
                {
                    this.GetData();
                }
                else
                {
                    this.UnsubscribeAsync();
                    this._dataTable = null;
                    this.Data = null;
                }
            }
        }

        public decimal Pnl
        {
            get
            {
                return this._pnl;
            }
            private set
            {
                this.SetValue(ref this._pnl, value);
            }
        }

        public override void Dispose()
        {
            this.IsActive = false;
            base.Dispose();
        }

        protected sealed override void OnConnectionStatusChanged()
        {
            base.OnConnectionStatusChanged();
            if (this.IsActive && this.ConnectionManager.ConnectionStatus == ConnectionStatus.Connected)
            {
                this.GetData();
            }
        }

        protected void ProcessChanges(IReadOnlyCollection<RowChangeBase> changes)
        {
            if (this._dataTable == null)
            {
                return;
            }
            IReadOnlyList<DataRow> wrapper = new ReadOnlyDataRowCollectionWrapper(this._dataTable.Rows);
            decimal pnl = this._pnl;
            foreach (RowChangeBase rowChange in changes)
            {
                if (rowChange.ChangeType == RowChangeType.Added)
                {
                    DataRow dataRow = this._dataTable.NewRow();
                    dataRow.ItemArray = ((RowAdded)rowChange).Data;
                    this._dataTable.Rows.InsertAt(dataRow, ~wrapper.BinarySearchByValue((string)rowChange.RowKey, row => row.Field<string>("Ticker")));
                    pnl += dataRow.Field<decimal>("Pnl");
                }
                else
                {
                    DataRow dataRow = this._dataTable.Rows.Find(rowChange.RowKey);
                    decimal rowPnl = dataRow.Field<decimal>("Pnl");
                    if (rowChange.ChangeType == RowChangeType.Removed)
                    {
                        dataRow.Delete();
                        pnl -= rowPnl;
                    }
                    else
                    {
                        foreach (ColumnChange columnChange in ((RowColumnsChanged)rowChange).ColumnChanges)
                        {
                            if (columnChange.ColumnName == "Pnl")
                            {
                                // Delta in Pnl
                                pnl += ((decimal)columnChange.Value - rowPnl);
                            }
                            dataRow[columnChange.ColumnName] = columnChange.Value;
                        }
                    }
                }
            }
            this._dataTable.AcceptChanges();
            this.Pnl = pnl;
        }

        protected abstract Task<DataTable> SubscribeAsync();

        protected abstract Task UnsubscribeAsync();

        private void GetData()
        {
            this.SubscribeAsync().ContinueWith(
                task => this.Dispatcher.Invoke(new Action<DataTable>(this.OnReceiveDataTable), task.Result),
                TaskContinuationOptions.NotOnFaulted
            );
        }

        private void OnReceiveDataTable(DataTable dataTable)
        {
            if (!this.IsActive)
            {
                return;
            }
            this.Data = new DataBoundObjectCollection(this._dataTable = dataTable);
            this.Pnl = dataTable.Rows.Cast<DataRow>().Sum(row => row.Field<decimal>("Pnl"));
        }
    }
}