using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace System.IO.SharedMemory
{
    /// <summary>
    /// A stream based on the memory mapped circular buffer. Allows to share data between process without lock.
    /// </summary>
    public class CircularBufferStream : Stream, IDisposable
    {
        private int _readTimeout = 1000;
        private int _writeTimeout = 1000;
        private CircularBuffer _circularBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBufferStream"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="nodeCount">The node.</param>
        /// <param name="nodeBufferSize">Size of the buffer.</param>
        public CircularBufferStream(string name, int nodeCount = 512, int nodeBufferSize = 32)
        {
            try
            {
                _circularBuffer = new CircularBuffer(name);
            }
            catch(FileNotFoundException)
            {
                _circularBuffer = new CircularBuffer(name, nodeCount, nodeBufferSize);
            }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get { return _circularBuffer != null && !_circularBuffer.ShuttingDown; }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get { return _circularBuffer != null && !_circularBuffer.ShuttingDown; }
        }

        /// <summary>
        /// Gets a value that determines whether the current stream can time out.
        /// </summary>
        public override bool CanTimeout
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
        /// </summary>
        public override int ReadTimeout
        {
            get
            {
                return _readTimeout;
            }
            set
            {
                _readTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out.
        /// </summary>
        public override int WriteTimeout
        {
            get
            {
                return _writeTimeout;
            }
            set
            {
                _writeTimeout = value;
            }
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// When overridden in a derived class, gets the length in bytes of the stream.
        /// </summary>
        public override long Length
        {
            get { return _circularBuffer.NodeBufferSize * (_circularBuffer.NodeCount - 1); }
        }

        /// <summary>
        /// When overridden in a derived class, gets or sets the position within the current stream.
        /// </summary>
        /// <exception cref="System.NotImplementedException">
        /// </exception>
        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">count or offset</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            int red = 0;
            while (red < count && _circularBuffer.HasNodeToRead)
            {
                int rd = _circularBuffer.Read(buffer, offset, _readTimeout);
                offset += rd;
                red += rd;
            }

#if DEBUG
            Debug.WriteLine(red + " byte(s) have been red from the underlying circular buffer.", "Information");
#endif

            return red;
        }

        /// <summary>
        /// When overridden in a derived class, sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position.</param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// When overridden in a derived class, sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">count or offset</exception>
        /// <exception cref="System.IO.IOException">If there is not enougth free space to write data.</exception>
        /// <exception cref="System.OutOfMemoryException">If the underlying buffer is full.</exception>
        /// <exception cref="System.TimeoutException">If it exceed the time allowed to write data.</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (!CanWrite)
                throw new NotSupportedException("Write is not supported.");

            byte[] tempBuffer;
            if (count < buffer.Length - offset)
            {
                tempBuffer = new byte[count];
                Array.Copy(buffer, offset, tempBuffer, 0, count);
            }
            else
            {
                tempBuffer = buffer;
            }

            int written = 0;
            int toWrite = tempBuffer.Length - offset;

            int freeSize = (_circularBuffer.FreeNodeCount) * _circularBuffer.NodeBufferSize;
#if DEBUG
            Debug.WriteLine("Buffer to write: " + tempBuffer.Length + " bytes, Free space available: " + _circularBuffer.FreeNodeCount + "x" + _circularBuffer.NodeBufferSize + "=" + freeSize + " bytes", "Information");
#endif
            if (freeSize < tempBuffer.Length)
                throw new IOException("Unable to write data into the stream, there is not enougth free space. (Data to write: " + tempBuffer.Length + " bytes, Free space available: " + _circularBuffer.FreeNodeCount + "x" + _circularBuffer.NodeBufferSize + "=" + freeSize + " bytes)");

            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (written < toWrite)
            {
                int wr = _circularBuffer.Write(tempBuffer, offset, _readTimeout);
                offset += wr;
                written += wr;

                if (!_circularBuffer.HasFreeNode && wr == 0)
                    throw new OutOfMemoryException("The underlying buffer is full.");

               if (sw.ElapsedMilliseconds > _writeTimeout) throw new TimeoutException(string.Format("Waited {0} miliseconds", _writeTimeout));
            }

#if DEBUG
            Debug.WriteLine(written + " byte(s) have been written to the underlying circular buffer.", "Information");
#endif
        }

        /// <summary>
        /// Releases all resources used by the <see cref="T:System.IO.Stream" />.
        /// </summary>
        void IDisposable.Dispose()
        {
            if (_circularBuffer != null)
            {
                _circularBuffer.Dispose();
            }
        }
    }
}
