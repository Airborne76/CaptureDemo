# UWP Screen Capture Demo
1803 UWP ScreenCapture api demo(need Windows10 Pro/Enterprise)，based  on Microsoft's sample. has bug with saving video.
Seems <code>Direct3D11CaptureFramePool.Create(_canvasDevice,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,_item.Size)</code> makes it can only write 2 frames to <code>MediaComposition</code>.😑
