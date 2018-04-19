using System;
using System.Collections.Generic;
using System.Threading;
using VirtualSpace.Shared;

namespace VirtualSpace.Bots
{
    internal class BotRunner
    {
        private const int EXIT_SUCCESS = 0;

        Bot[] bots = null;

        public static void Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.Clear();

            BotRunner botRunner = new BotRunner();
            botRunner._run();
        }

        ~ BotRunner()
        {
            Terminate();
            Logger.Info("All bots stopped.");
        }

        private void _run()
        {
            Logger.SetLevel(Logger.Level.Debug);

            Polygon[] requiredSpace = new Polygon[]
            {
                    new Polygon(
                        new List<Vector>()
                        {
                            new Vector(-2.0, -2.0),
                            new Vector(0.5, -2.0),
                            new Vector(0.5, 2.0),
                            new Vector(-2.0, 2.0)
                        }),
                    new Polygon(
                        new List<Vector>()
                        {
                            new Vector(2.0, -2.0),
                            new Vector(-0.5, -2.0),
                            new Vector(-0.5, 2.0),
                            new Vector(2.0, 2.0)
                        })
            };

            Movement[] movements = new Movement[] {
                new RequestState(),
                new RequestState(),
                new RequestState(),
                //new RequestState(), 
                //new RequestRandom(),
                //new RequestRandom(),
                //new RequestRandom(),
                //new RequestRandom(),
                //new RequestToMoveFromAToB(true),
                //new RequestToMoveFromAToB(false),
                //new RequestToMoveFromAToBToCToD(0),
                //new RequestToMoveFromAToBToCToD(1),
                //new RequestToMoveFromAToBToCToD(2),
                //new RequestToMoveFromAToBToCToD(3),
                //new IdleMovement(1f, -1f),
                //new IdleMovement(-1.0f, -1.7f),
                //new IdleMovement(1f, 1f),
                //new IdleMovement(-1.0f, 1f),
                //new MoveTowardsPosition(),
                //new MoveTowardsPosition(),
                //new MoveTowardsPosition(),
                //new MoveTowardsPosition(),
                //new BouncingMovement2D(1.5f, 1.0f, -0.02f, -0.01f, requiredSpace[0]),
                //new BouncingMovement2D(2.5f, 1.0f, -0.02f, -0.016f, requiredSpace[1])
            };

            Logger.Info("Starting bots.");
            Logger.Info("Press any key to terminate.");
            Logger.Info("===============");
            Logger.Info("Bot logs:");

            bots = new Bot[movements.Length];
            for (int index = 0; index < bots.Length; index++)
            {
                Thread.Sleep(5000);

                bots[index] = new Bot(movements[index]);
            }

            Console.ReadKey();

            Environment.Exit(EXIT_SUCCESS);
        }

        private void Terminate()
        {
            if (bots != null)
                foreach (Bot bot in bots) bot.Deactivate();
        }
    }
}