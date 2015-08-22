using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LongRunningTask
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private Random _rnd;
        private List<string> _largeList;
        private CompositeDisposable _cleanUp;

        private MainModel _model;
        public MainViewModel(MainModel model)
        {
            _model = model;
            _largeList = _model.GetLargeList();

            var input = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            h => PropertyChanged += h,
            h => PropertyChanged -= h)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOnDispatcher()
                .Select(x => _searchText)
                .Where(text => text != null && text != String.Empty)//Limit empty queries
                .DistinctUntilChanged(); //Only query if the value has changed

            var search = Observable.ToAsync<string, IEnumerable<string>>(ExecuteListSearch);

            var results = from searchTerm in input
                          from result in search(searchTerm).TakeUntil(input)
                          select result;

            var resultsDispose = results.ObserveOnDispatcher()
                                        .Subscribe(res =>
                                        {
                                            Console.WriteLine("TEST");
                                            _hints.Clear();
                                            var tempList = res.ToList();
                                            foreach (var i in tempList)
                                            {
                                                _hints.Add(i);
                                            }
                                        });

            //Load a long running task, the screen will not freeze as the task is executing
            var asyncDispose = Observable.FromAsync(() => _model.GetLargeListAsyncWithDelay())
                                         .ObserveOnDispatcher() //Run on the dispatcher
                                         .Subscribe(x => 
                                         {
                                             Console.WriteLine("Added To UI on thread id " + Thread.CurrentThread.ManagedThreadId);
                                             var tempList = x.ToList();
                                             foreach (var item in tempList)
                                                 _modelHints.Add(item);
                                         });

            //Turn a method to Async and load into UI
            var sycDelayDispose = Observable.ToAsync(() => _model.GetStringWithDelay())()
                                            .ObserveOnDispatcher()
                                            .Subscribe(o => 
                                                {
                                                    Console.WriteLine("Sync Result on thread id " + Thread.CurrentThread.ManagedThreadId);
                                                     LongRunString = o;
                                                });

            _cleanUp = new CompositeDisposable(resultsDispose, asyncDispose);
        }

        private readonly ObservableCollection<string> _hints = new ObservableCollection<string>();
        public ObservableCollection<string> Hints
        {
            get { return _hints; }
        }

        private IEnumerable<string> ExecuteListSearch(string search)
        {
            var result = _largeList.Where(t => t.Contains(search)).ToList();

            return result;
        }

        private string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value;
                OnPropertyChanged("SearchText");
            }
        }

        private string _longRunString;
        public string LongRunString
        {
            get { return _longRunString; }
            set
            {
                _longRunString = value;
                OnPropertyChanged("LongRunString");
            }
        }


        private readonly ObservableCollection<string> _modelHints = new ObservableCollection<string>();
        public ObservableCollection<string> ModelHints
        {
            get { return _modelHints; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}
