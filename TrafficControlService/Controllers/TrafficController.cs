﻿namespace TrafficControlService.Controllers;

[ApiController]
[Route("")]
public class TrafficController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IVehicleStateRepository _vehicleStateRepository;
    private readonly ILogger<TrafficController> _logger;
    private readonly ISpeedingViolationCalculator _speedingViolationCalculator;
    private readonly string _roadId;

    public TrafficController(
        ILogger<TrafficController> logger,
        HttpClient httpClient,
        IVehicleStateRepository vehicleStateRepository,
        ISpeedingViolationCalculator speedingViolationCalculator)
    {
        _logger = logger;
        _httpClient = httpClient;
        _vehicleStateRepository = vehicleStateRepository;
        _speedingViolationCalculator = speedingViolationCalculator;
        _roadId = speedingViolationCalculator.GetRoadId();
    }

    [HttpPost("entrycam")]
    public async Task<ActionResult> VehicleEntry(VehicleRegistered msg)
    {
        try
        {
            // log entry
            _logger.LogInformation($"ENTRY detected in lane {msg.Lane} at {msg.Timestamp.ToString("hh:mm:ss")} " +
                $"of vehicle with license-number {msg.LicenseNumber}.");

            // store vehicle state
            var vehicleState = new VehicleState
            {
                LicenseNumber = msg.LicenseNumber,
                EntryTimestamp = msg.Timestamp
            };
            await _vehicleStateRepository.SaveVehicleStateAsync(vehicleState);

            return Ok();
        }
        catch
        {
            return StatusCode(500);
        }
    }

    [HttpPost("exitcam")]
    public async Task<ActionResult> VehicleExit(VehicleRegistered msg, [FromServices] DaprClient daprClient)
    {
        try
        {
            // get vehicle state
            var vehicleState = await _vehicleStateRepository.GetVehicleStateAsync(msg.LicenseNumber);
            if (!vehicleState.HasValue)
            {
                return NotFound();
            }

            // log exit
            _logger.LogInformation($"EXIT detected in lane {msg.Lane} at {msg.Timestamp.ToString("hh:mm:ss")} " +
                $"of vehicle with license-number {msg.LicenseNumber}.");

            // update state
            vehicleState = vehicleState.Value with { ExitTimestamp = msg.Timestamp };
            await _vehicleStateRepository.SaveVehicleStateAsync(vehicleState.Value);

            // handle possible speeding violation
            int violation = _speedingViolationCalculator.DetermineSpeedingViolationInKmh(
                vehicleState.Value.EntryTimestamp, vehicleState.Value.ExitTimestamp.Value);
            if (violation > 0)
            {
                _logger.LogInformation($"Speeding violation detected ({violation} KMh) of vehicle" +
                    $"with license-number {vehicleState.Value.LicenseNumber}.");

                var speedingViolation = new SpeedingViolation
                {
                    VehicleId = msg.LicenseNumber,
                    RoadId = _roadId,
                    ViolationInKmh = violation,
                    Timestamp = msg.Timestamp
                };

                // publish speedingviolation
                //var message = JsonContent.Create<SpeedingViolation>(speedingViolation);
                //await _httpClient.PostAsync("http://localhost:6001/collectfine", message);
                //await _httpClient.PostAsync("http://localhost:3600/v1.0/publish/pubsub/speedingviolations", message);

                await daprClient.PublishEventAsync("pubsub", "speedingviolations", speedingViolation);
            }

            return Ok();
        }
        catch
        {
            return StatusCode(500);
        }
    }
}
