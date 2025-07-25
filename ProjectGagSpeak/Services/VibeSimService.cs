//using GagSpeak.Services.Configs;
//using GagSpeak.Toybox;
//using NAudio.Wave;
//using NAudio.Wave.SampleProviders;

//namespace GagSpeak.Services;

///// <summary>
/////     <b> TODO: Migrate this format from the audio player to the scd effect spawner if possible. Or keep this if more accurate. </b><para/>
/////     
/////     This is a scuffed port from the old gagspeak to get something working by open beta. <para/>
/////     
/////     This is susceptible to audio scrapes when hitting the lowest volume or the highest volume.
/////     Going above and below the max threshold causes this, and is why migrating to an scd may be better.
///// </summary>
///// <remarks>
/////     If anything should be fixed for QoL it would be finding a better waveplayer, as this
/////     scratches audio when hitting 0.0 and 1.0 thresholds.
///// </remarks>
//public sealed class VibeSimService
//{
//    private readonly ILogger<VibeSimService> _logger;
//    private IWavePlayer waveOutDevice;
//    private AudioFileReader audioFileReader;
//    private VibeSimLooper loopStream;
//    private SmbPitchShiftingSampleProvider pitchShifter; // emulates vibrator intensity rising and falling.
//    public bool isPlaying;

//    private bool isAdjustingVolume = false;
//    private bool isAdjustingPitch = false;
//    private float targetVolume = 0.01f;
//    private float targetPitch = 1.0f;
//    private const float volumeChangeRate = 0.025f; // Adjust this value as needed
//    private const float pitchChangeRate = 0.025f; // Adjust this value as needed


//    public List<string> PlaybackDevices { get; private set; } = new List<string> { "Using Default Device!" };
//    public int ActivePlaybackDeviceId { get; set; } = 0;


//    public VibeSimService(ILogger<VibeSimService> logger)
//    {
//        _logger = logger;
//        StartupNAudioProvider("vibratorQuiet.wav");
//    }

//    protected void StartupNAudioProvider(string audioPathInput, bool isDeviceChange = false)
//    {
//        // get the audio file
//        var audioPath = Path.Combine(ConfigFileProvider.AssemblyDirectory, "Assets", audioPathInput);
//        audioFileReader = new AudioFileReader(audioPath);
//        loopStream = new VibeSimLooper(audioFileReader);
//        pitchShifter = new SmbPitchShiftingSampleProvider(loopStream.ToSampleProvider());
//        var waveProvider = pitchShifter.ToWaveProvider16();
//        // if device change is false, dont input the ID
//        if (isDeviceChange == false)
//        {
//            waveOutDevice = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 }; // 100ms latency, 2 buffers, device id -1 expected.
//        }
//        else
//        {
//            waveOutDevice = new WaveOutEvent { DeviceNumber = ActivePlaybackDeviceId - 1, DesiredLatency = 80, NumberOfBuffers = 3 };
//        }
//        // now try and initalize it
//        try
//        {
//            // if devicechange is false, assume we are initializing
//            _logger.LogDebug($"Detected Device Count: {WaveOut.DeviceCount}", LoggerType.Toys);
//            // see what device is currently selected
//            for (var i = 0; i < WaveOut.DeviceCount; i++)
//            {
//                var capabilities = WaveOut.GetCapabilities(i);
//                _logger.LogTrace($"Device {i}: {capabilities.ProductName}", LoggerType.Toys);
//                PlaybackDevices.Add(capabilities.ProductName); // add the device name to the list
//            }

//            waveOutDevice.Init(waveProvider);
//            _logger.LogInformation("SoundPlayer sucessfully setup with NAudio", LoggerType.Toys);
//        }
//        catch (NAudio.MmException ex)
//        {
//            if (ex.Result == NAudio.MmResult.BadDeviceId)
//            {
//                // Handle the exception, e.g. show a message to the user
//                _logger.LogError("Bad Default Device ID. Attempting manual assignment.");

//                // attempt to do it manually
//                for (var i = 0; i < WaveOut.DeviceCount; i++)
//                {
//                    try
//                    {
//                        var capabilities = WaveOut.GetCapabilities(i);
//                        _logger.LogTrace($"Device {i}: {capabilities.ProductName}\n" +
//                        $" --- Supports Playback Control: {capabilities.SupportsPlaybackRateControl}", LoggerType.Toys);

//                        waveOutDevice = new WaveOutEvent { DeviceNumber = i, DesiredLatency = 80, NumberOfBuffers = 3 };
//                        waveOutDevice.Init(waveProvider);
//                        _logger.LogTrace("SoundPlayer successfully setup with NAudio for device " + i, LoggerType.Toys);
//                        // if we reach here, the device is valid and we can break the loop
//                        ActivePlaybackDeviceId = i + 1;
//                        break;
//                    }
//                    catch (NAudio.MmException ex2)
//                    {
//                        if (ex2.Result == NAudio.MmResult.BadDeviceId)
//                        {
//                            // Handle the exception, e.g. show a message to the user
//                            _logger.LogError($"Bad Device ID for device {i}, trying next device.");
//                        }
//                        else
//                        {
//                            _logger.LogError("Unknown NAudio Exception: " + ex2.Message);
//                            throw;
//                        }
//                    }
//                }
//            }
//            else
//            {
//                _logger.LogError("Unknown NAudio Exception: " + ex.Message);
//                throw;
//            }
//        }
//    }

//    public void SwitchDevice(int deviceId)
//    {
//        if (deviceId < 0 || deviceId >= WaveOut.DeviceCount)
//        {
//            _logger.LogError($"Invalid device ID: {deviceId}");
//            return;
//        }

//        var wasActiveBeforeChange = isPlaying;
//        if (isPlaying)
//        {
//            waveOutDevice.Stop();
//        }

//        waveOutDevice.Dispose();

//        // change the activeDeviceId
//        ActivePlaybackDeviceId = deviceId;

//        waveOutDevice = new WaveOutEvent { DeviceNumber = deviceId - 1, DesiredLatency = 80, NumberOfBuffers = 3 };
//        waveOutDevice.Init(pitchShifter.ToWaveProvider16());
//        _logger.LogDebug($"Switched to device {deviceId}: {PlaybackDevices[deviceId]}", LoggerType.Toys);

//        if (wasActiveBeforeChange)
//        {
//            waveOutDevice.Play();
//        }
//    }

//    public void ChangeAudioPath(string audioPath)
//    {
//        // Stop the current audio
//        var wasActiveBeforeChange = isPlaying;
//        if (isPlaying)
//        {
//            waveOutDevice.Stop();
//        }
//        // Dispose of the current resources
//        waveOutDevice.Dispose();
//        audioFileReader.Dispose();
//        loopStream.Dispose();

//        StartupNAudioProvider(audioPath);

//        // If the audio was playing before, start it again
//        if (wasActiveBeforeChange)
//        {
//            waveOutDevice.Play();
//        }
//    }

//    public void Play()
//    {
//        isPlaying = true;
//        audioFileReader.Volume = 0f;
//        waveOutDevice.Play();
//    }

//    public void Stop()
//    {
//        isPlaying = false;
//        waveOutDevice.Stop();
//    }

//    public async void SetVolume(float intensity)
//    {
//        targetVolume = intensity;
//        targetPitch = 1.0f + intensity * .5f;

//        // If the volume or pitch is already being adjusted, don't start another adjustment
//        if (isAdjustingVolume || isAdjustingPitch) return;

//        isAdjustingVolume = true;
//        isAdjustingPitch = true;

//        while (Math.Abs(audioFileReader.Volume - targetVolume) > volumeChangeRate || Math.Abs(pitchShifter.PitchFactor - targetPitch) > pitchChangeRate)
//        {
//            if (audioFileReader.Volume < targetVolume)
//            {
//                audioFileReader.Volume += volumeChangeRate;
//            }
//            else if (audioFileReader.Volume > targetVolume)
//            {
//                audioFileReader.Volume -= volumeChangeRate;
//            }

//            if (pitchShifter.PitchFactor < targetPitch)
//            {
//                pitchShifter.PitchFactor += pitchChangeRate;
//            }
//            else if (pitchShifter.PitchFactor > targetPitch)
//            {
//                pitchShifter.PitchFactor -= pitchChangeRate;
//            }

//            await Task.Delay(20); // Adjust this delay as needed
//        }

//        // Once the volume and pitch are close enough to the target, set them directly
//        audioFileReader.Volume = targetVolume;
//        pitchShifter.PitchFactor = targetPitch;
//        isAdjustingVolume = false;
//        isAdjustingPitch = false;
//    }

//    public void Dispose()
//    {
//        // Stop the audio and dispose of the resources
//        waveOutDevice.Stop();
//        waveOutDevice.Dispose();
//        audioFileReader.Dispose();
//        loopStream.Dispose();
//    }
//}
