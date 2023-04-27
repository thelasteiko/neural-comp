using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeurCLib;

namespace NeurCApp;

public class StreamListener {
  public StreamListener() {
    Controller.OnStreamData onStream = new(Stream_OnStreamData);
  }
  public static void Stream_OnStreamData(byte[] data) {

  }
}
