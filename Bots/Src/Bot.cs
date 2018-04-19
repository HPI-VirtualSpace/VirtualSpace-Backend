using System;
using System.Timers;

namespace VirtualSpace.Bots
{
    internal class Bot
    {
        private BotWorker _worker;
        public bool Active
        {
            get => true;
        }
        //Timer aTimer;

        public void Deactivate()
        {
            _worker.Stop();
        }

        //private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        //{
        //    _deactivate();
        //    aTimer.Stop();
        //}

        public Bot(Movement movement)
        {
            _worker = new BotWorker(movement, Deactivate);
            _worker.Start();

            //aTimer = new Timer()
            //{
            //    Interval = 5000
            //};
            //aTimer.Elapsed += OnTimedEvent;
            //aTimer.Enabled = true;
        }
    }
}