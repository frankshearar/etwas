﻿[<AutoOpen>]
module Debug

#if DEBUG
let (|>) value func =
  let result = func value
  result
#endif