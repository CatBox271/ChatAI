using System;
using System.Collections;
using System.Collections.Generic;

public interface Itool
{
    public IEnumerator DealToolCallsCoroutine(List<ToolCall> toolCalls, Action<List<DeepSeekMessage>> onComplete);
}
