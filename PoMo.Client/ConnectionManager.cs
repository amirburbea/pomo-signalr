using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using PoMo.Common.DataObjects;
using ConnectionState = Microsoft.AspNet.SignalR.Client.ConnectionState;

namespace PoMo.Client
{
    public interface IConnectionManager
    {
        event EventHandler ConnectionStatusChanged;

        event EventHandler<ChangeEventArgs> FirmSummaryChanged;

        event EventHandler<PortfolioChangeEventArgs> PortfolioChanged;

        ConnectionStatus ConnectionStatus
        {
            get;
        }

        Task<PortfolioModel[]> GetPortfoliosAsync();

        Task<DataTable> SubscribeToFirmSummaryAsync();

        Task<DataTable> SubscribeToPortfolioAsync(string portfolioId);

        Task UnsubscribeFromFirmSummaryAsync();

        Task UnsubscribeFromPortfolioAsync(string portfolioId);
    }

    internal sealed class ConnectionManager : IConnectionManager, IDisposable
    {
        private readonly string _baseUri;
        private readonly HubConnection _hubConnection;
        private readonly IHubProxy _hubProxy;
        private readonly Dispatcher _dispatcher;
        private readonly JsonSerializer _jsonSerializer;
        private bool _isDisposed;

        public ConnectionManager(IWebSettings webSettings, Dispatcher dispatcher, JsonSerializer jsonSerializer)
        {
            ServicePointManager.DefaultConnectionLimit = 10; // Needed for SignalR client in WPF.
            this._dispatcher = dispatcher;
            this._jsonSerializer = jsonSerializer;
            this._baseUri = $"http://{webSettings.Host}:{webSettings.Port}/pomo/";
            this._hubConnection = new HubConnection(this._baseUri + "signalr") { JsonSerializer = jsonSerializer };
            this._hubProxy = this._hubConnection.CreateHubProxy("data");
            this._hubProxy.On<RowChangeBase[]>(nameof(this.OnFirmSummaryChanged), this.OnFirmSummaryChanged);
            this._hubProxy.On<string, RowChangeBase[]>(nameof(this.OnPortfolioChanged), this.OnPortfolioChanged);
            this._hubConnection.StateChanged += this.HubConnection_StateChanged;
            this._hubConnection.Start();
            this._dispatcher.InvokeAsync(this.OnConnectionStatusChanged, DispatcherPriority.Background);
        }

        public event EventHandler ConnectionStatusChanged;

        public event EventHandler<ChangeEventArgs> FirmSummaryChanged;

        public event EventHandler<PortfolioChangeEventArgs> PortfolioChanged;

        public ConnectionStatus ConnectionStatus
        {
            get
            {
                switch (this._hubConnection.State)
                {
                    case ConnectionState.Connected:
                        return ConnectionStatus.Connected;
                    case ConnectionState.Connecting:
                    case ConnectionState.Reconnecting:
                        return ConnectionStatus.Connecting;
                }
                return ConnectionStatus.Disconnected;
            }
        }

        public void Dispose()
        {
            this._isDisposed = true;
            this._hubConnection.StateChanged -= this.HubConnection_StateChanged;
            if (this._hubConnection.State != ConnectionState.Connected)
            {
                return;
            }
            try
            {
                this._hubConnection.Stop(TimeSpan.FromSeconds(1d));
            }
            catch (Exception)
            {
                // Do nothing.
            }
        }

        public async Task<PortfolioModel[]> GetPortfoliosAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                string text = await client.GetStringAsync(this._baseUri + "api/portfolios").ConfigureAwait(false);
                using (TextReader textReader = new StringReader(text))
                {
                    using (JsonTextReader jsonReader = new JsonTextReader(textReader))
                    {
                        return this._jsonSerializer.Deserialize<PortfolioModel[]>(jsonReader);
                    }
                }
            }
        }

        public Task<DataTable> SubscribeToFirmSummaryAsync()
        {
            return this._hubProxy.Invoke<DataTable>("SubscribeToFirmSummary");
        }

        public Task<DataTable> SubscribeToPortfolioAsync(string portfolioId)
        {
            return this._hubProxy.Invoke<DataTable>("SubscribeToPortfolio", portfolioId);
        }

        public Task UnsubscribeFromFirmSummaryAsync()
        {
            return this._hubProxy.Invoke("UnsubscribeFromFirmSummary");
        }

        public Task UnsubscribeFromPortfolioAsync(string portfolioId)
        {
            return this._hubProxy.Invoke("UnsubscribeFromPortfolio", portfolioId);
        }

        private void HubConnection_StateChanged(StateChange state)
        {
            if (this._isDisposed)
            {
                return;
            }
            this.OnConnectionStatusChanged();
            if (state.NewState == ConnectionState.Disconnected)
            {
                this.OnDisconnected();
            }
        }

        private void OnConnectionStatusChanged()
        {
            this.ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnDisconnected()
        {
            if (this._isDisposed)
            {
                return;
            }
            Task start = this._hubConnection.Start();
            this._dispatcher.InvokeAsync(this.OnConnectionStatusChanged, DispatcherPriority.Background);
            bool success = start.ContinueWith(task => !task.IsFaulted, TaskScheduler.Default).Result;
            if (success)
            {
                return;
            }
            Task.Factory.StartNew(
                () =>
                {
                    // Waiting five seconds to reconnect but checking for disposal or connection forced some other way every second.
                    for (int index = 0; index < 5; index++)
                    {
                        if (this._isDisposed || this._hubConnection.State != ConnectionState.Disconnected)
                        {
                            return;
                        }
                        Thread.Sleep(TimeSpan.FromSeconds(1d));
                    }
                    if (!this._isDisposed && this._hubConnection.State == ConnectionState.Disconnected)
                    {
                        this.OnDisconnected();
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default
            );
        }

        private void OnFirmSummaryChanged(IReadOnlyCollection<RowChangeBase> changes)
        {
            this.FirmSummaryChanged?.Invoke(this, new ChangeEventArgs(changes));
        }

        private void OnPortfolioChanged(string portfolioId, IReadOnlyCollection<RowChangeBase> changes)
        {
            this.PortfolioChanged?.Invoke(this, new PortfolioChangeEventArgs(portfolioId, changes));
        }
    }
}