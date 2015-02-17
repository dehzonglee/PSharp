﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace ParsingTest
{
    internal machine Client
    {
        private Machine Server;
        private int Counter;

        [Initial]
        private state Init
        {
            entry
            {
                this.Server = (Machine)this.Payload;
                this.Counter = 0;
                raise Unit;
            }

            on Unit goto Playing;
        }

        private state Playing
        {
            entry
            {
                if (this.Counter == 5)
                {
                    send Stop to this.Server;
                    this.StopIt();
                }
            }

            on Unit goto Playing;
            on Pong do SendPing;
            on Stop do StopIt;
        }

        private action SendPing
        {
            this.Counter++;
            Console.WriteLine("\nTurns: {0} / 5\n", this.Counter);
            send Ping to this.Server;
            raise Unit;
        }

        private action StopIt
        {
            Console.WriteLine("Client stopped.\n");
            delete;
        }
    }
}
