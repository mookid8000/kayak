﻿using System;
using System.Collections.Generic;
using System.Text;
using Kayak.Http;
using Kayak.Tests.Net;

namespace Kayak.Tests
{
    class MockSocket : ISocket
    {
        public bool GotEnd;
        public bool GotDispose;
        public DataBuffer Buffer;
        public Action Continuation;

        public MockSocket()
        {
            Buffer = new DataBuffer();
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            // XXX do copy? 
            Buffer.AddToBuffer(data);

            if (continuation != null)
            {
                Continuation = continuation;
                return true;
            }

            return false;
        }

        public System.Net.IPEndPoint RemoteEndPoint
        {
            get { throw new NotImplementedException(); }
        }

        public void Connect(System.Net.IPEndPoint ep)
        {
            throw new NotImplementedException();
        }

        public void End()
        {
            if (GotEnd)
                throw new Exception("OnEnd was called more than once.");

            GotEnd = true;
        }

        public void Dispose()
        {
            if (GotDispose)
                throw new Exception("Dispose was called more than once.");

            GotDispose = true;
        }
    }

    class MockDataProducer : IDataProducer
    {
        public Action DisposeAction = null;
        public bool WasDisposed;
        Func<IDataConsumer, IDisposable> connect;

        public static MockDataProducer Create(IEnumerable<string> data, bool sync, Action dispose, Action<Action> subscribe)
        {
            return new MockDataProducer(dc =>
            {
                subscribe(() =>
                    WriteNext(data.GetEnumerator(), dc, sync));

                return new Disposable(dispose);
            });
        }

        static void WriteNext(IEnumerator<string> data, IDataConsumer c, bool sync)
        {
            while (true)
            {
                if (!data.MoveNext())
                {
                    c.OnEnd();
                    data.Dispose();
                    break;
                }

                var seg = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data.Current));
                if (sync)
                {
                    if (c.OnData(seg, null))
                        throw new Exception("sync write should have returned false");
                }
                else
                    if (c.OnData(
                        seg, 
                        () => WriteNext(data, c, sync)))
                        break;
            }
        }

        public MockDataProducer(Func<IDataConsumer, IDisposable> connect)
        {
            this.connect = connect;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            var d = connect(channel);
            if (d == null)
                return new Disposable(() =>
                {    
                    if (DisposeAction != null)
                        DisposeAction();

                    WasDisposed = true;
                });
            return d;
        }
    }

    class MockDataConsumer : IDataConsumer
    {
        public Action<ArraySegment<byte>> OnDataAction;
        public Action OnEndAction;
        public DataBuffer Buffer;
        public Action Continuation;
        public Exception Exception;
        public bool GotEnd;

        public MockDataConsumer()
        {
            Buffer = new DataBuffer();
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            // XXX do copy? 
            Buffer.AddToBuffer(data);

            if (OnDataAction != null)
                OnDataAction(data);

            if (continuation != null)
            {
                Continuation = continuation;
                return true;
            }

            return false;
        }

        public void OnError(Exception e)
        {
            Exception = e;
        }
        
        public void OnEnd()
        {
            if (GotEnd)
                throw new Exception("End was already called.");

            GotEnd = true;

            if (OnEndAction != null)
                OnEndAction();
        }
    }

    class MockHttpServerTransactionDelegate : IHttpServerTransactionDelegate
    {
        public class HttpRequest
        {
            public HttpRequestHead Head;
            public bool ShouldKeepAlive;
            public DataBuffer Data;
        }

        public List<HttpRequest> Requests;
        public Func<ArraySegment<byte>, Action, bool> OnDataAction;
        public Exception Exception;
        public bool GotOnEnd;
        public bool GotDispose;
        public HttpRequest current; // XXX rename

        public MockHttpServerTransactionDelegate()
        {
            Requests = new List<HttpRequest>();
        }

        public void OnRequest(HttpRequestHead request, bool shouldKeepAlive)
        {
            current = new HttpRequest() { Head = request, ShouldKeepAlive = shouldKeepAlive, Data = new DataBuffer() };
        }

        public bool OnRequestData(ArraySegment<byte> data, Action continuation)
        {
            current.Data.AddToBuffer(data);

            if (OnDataAction != null)
                return OnDataAction(data, continuation);

            return false;
        }

        public void OnRequestEnd()
        {
            Requests.Add(current);
        }

        public void OnError(Exception e)
        {
            Exception = e;
        }

        public void OnEnd()
        {
            if (GotOnEnd)
                throw new Exception("OnEnd was called more than once.");

            GotOnEnd = true;
        }

        public void Dispose()
        {
            if (GotDispose)
                throw new Exception("OnEnd was called more than once.");

            GotDispose = true;
        }
    }
}
