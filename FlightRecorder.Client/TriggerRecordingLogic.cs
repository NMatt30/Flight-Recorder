using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FlightRecorder.Client;
public class IIRFilter
{
    private double alpha;
    private double lastFiltered = 0;
    private bool isFirstRun = true;

    public IIRFilter(double alpha)
    {
        this.alpha = alpha;
    }

    public double Filter(double current)
    {
        if (isFirstRun)
        {
            lastFiltered = current;
            isFirstRun = false;
        }
        else
        {
            lastFiltered = alpha * current + (1 - alpha) * lastFiltered;
        }

        return lastFiltered;
    }
}


public class TriggerRecordingLogic
{
    private enum AircraftState
    {
        Stopped,
        StartDepartureRecording,
        Departing,
        StopDepartureRecording,
        SaveDepartureRecording,
        Flying,
        StartArrivalRecording,
        Arriving,
        StopArrivalRecording,
        SaveArrivalRecording,
        Landed
    }
    private AircraftState aircraftState;

    private int flightInitiatedAltitude = 2500;
    private int landingTransitionAltitude = 1500;
    private int landingVSThreshold = -250;
    private double filteredVerticalSpeed = 0;
    private bool recordTakeoff = false;
    private bool recordLanding = false;

    private readonly StateMachine stateMachine;
    private IIRFilter vsFilter;

    public int FlightInitiatedAltitude { get { return flightInitiatedAltitude; } set { flightInitiatedAltitude = value;} }
    public int LandingTransitionAltitude { get { return landingTransitionAltitude; } set { landingTransitionAltitude = value;} }
    public int LandingVSThreshold { get { return landingVSThreshold; } set { landingVSThreshold= value;} }
    public bool RecordTakeoff { get { return recordTakeoff; } set { recordTakeoff = value; } }
    public bool RecordLanding { get { return recordLanding; } set { recordLanding = value; } }

    private bool IsInReplayMode => (stateMachine.CurrentState == StateMachine.State.ReplayingSaved) | (stateMachine.CurrentState == StateMachine.State.ReplayingUnsaved);

    public TriggerRecordingLogic(StateMachine stateMachine) 
    {
        this.stateMachine = stateMachine;
        aircraftState = AircraftState.Stopped;

        vsFilter = new IIRFilter(0.1);
    }
    public void ProcessAircraftState(AircraftPositionStruct? position)
    {
        if (!IsInReplayMode)
        {
            filteredVerticalSpeed = vsFilter.Filter((double)position?.VerticalSpeed) * 60;
            switch (aircraftState)
            {
                case AircraftState.Stopped:
                    if (position?.GroundSpeed > 10 &&
                        position?.IsOnGround == 1)
                    {
                        if (RecordTakeoff)
                        {
                            aircraftState = AircraftState.StartDepartureRecording;
                        }
                        else
                        {
                            aircraftState = AircraftState.Departing;
                        }
                    }
                    break;

                case AircraftState.StartDepartureRecording:
                    stateMachine.TransitFromShortcutAsync(StateMachine.Event.Record);
                    aircraftState = AircraftState.Departing;
                    break;

                case AircraftState.Departing:
                    if (position?.AltitudeAboveGround > landingTransitionAltitude)
                    {
                        if (RecordTakeoff)
                        {
                            aircraftState = AircraftState.StopDepartureRecording;
                        }
                        else
                        {
                            aircraftState = AircraftState.Flying;
                        }
                    }
                    break;

                case AircraftState.StopDepartureRecording:
                    stateMachine.TransitFromShortcutAsync(StateMachine.Event.Stop);
                    aircraftState = AircraftState.SaveDepartureRecording;
                    break;

                case AircraftState.SaveDepartureRecording:
                    stateMachine.TransitFromShortcutAsync(StateMachine.Event.Save);
                    aircraftState = AircraftState.Flying;
                    break;

                case AircraftState.Flying:
                    if (position?.AltitudeAboveGround < landingTransitionAltitude &&
                       filteredVerticalSpeed <= landingVSThreshold)
                    {
                        if (RecordLanding)
                        {
                            aircraftState = AircraftState.StartArrivalRecording;
                        }
                        else
                        {
                            aircraftState = AircraftState.Arriving;
                        }

                    }
                    break;

                case AircraftState.StartArrivalRecording:
                    stateMachine.TransitFromShortcutAsync(StateMachine.Event.Record);
                    aircraftState = AircraftState.Arriving;
                    break;

                case AircraftState.Arriving:
                    if (position?.GroundSpeed == 0 &&
                       position?.IsOnGround == 1)
                    {
                        if (RecordLanding)
                        {
                            aircraftState = AircraftState.StopArrivalRecording;
                        }
                        else
                        {
                            aircraftState = AircraftState.Landed;
                        }
                    }
                    break;

                case AircraftState.StopArrivalRecording:
                    stateMachine.TransitFromShortcutAsync(StateMachine.Event.Stop);
                    aircraftState = AircraftState.SaveArrivalRecording;
                    break;

                case AircraftState.SaveArrivalRecording:
                    stateMachine.TransitFromShortcutAsync(StateMachine.Event.Save);
                    aircraftState = AircraftState.Landed;
                    break;

                case AircraftState.Landed:
                    aircraftState = AircraftState.Stopped;
                    break;
            }
        }
    }
}

