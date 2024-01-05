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
        Landed,
        Flying,
        Record,
        Landing
    }
    private AircraftState aircraftState;

    private int flightInitiatedAltitude = 2500;
    private int landingTransitionAltitude = 1500;
    private int landingVSThreshold = -250;
    private double filteredVerticalSpeed = 0;

    private readonly StateMachine stateMachine;
    private IIRFilter vsFilter;

    public int FlightInitiatedAltitude { get { return flightInitiatedAltitude; } set { flightInitiatedAltitude = value;} }
    public int LandingTransitionAltitude { get { return landingTransitionAltitude; } set { landingTransitionAltitude = value;} }
    public int LandingVSThreshold { get { return landingVSThreshold; } set { landingVSThreshold= value;} }

    private bool IsInReplayMode => (stateMachine.CurrentState == StateMachine.State.ReplayingSaved) | (stateMachine.CurrentState == StateMachine.State.ReplayingUnsaved);

    public TriggerRecordingLogic(StateMachine stateMachine) 
    {
        this.stateMachine = stateMachine;
        aircraftState = AircraftState.Landed;

        vsFilter = new IIRFilter(0.1);
    }

    public void ProcessAircraftState(AircraftPositionStruct? position)
    {
        if (!IsInReplayMode)
        {
            filteredVerticalSpeed = vsFilter.Filter((double)position?.VerticalSpeed) * 60;
            switch (aircraftState)
            {
                case AircraftState.Landed:
                    if (position?.AltitudeAboveGround >= flightInitiatedAltitude)
                    {
                        aircraftState = AircraftState.Flying;
                    }
                    break;

                case AircraftState.Flying:
                    if (position?.AltitudeAboveGround < landingTransitionAltitude &&
                        filteredVerticalSpeed <= landingVSThreshold)
                    {
                        aircraftState = AircraftState.Record;
                    }
                    break;

                case AircraftState.Record:
                    stateMachine.TransitFromShortcutAsync(StateMachine.Event.Record);
                    aircraftState = AircraftState.Landing;
                    break;

                case AircraftState.Landing:
                    if (position?.GroundSpeed == 0 &&
                        position?.IsOnGround == 1)
                    {
                        stateMachine.TransitFromShortcutAsync(StateMachine.Event.Stop);
                        stateMachine.TransitFromShortcutAsync(StateMachine.Event.Save);
                        aircraftState = AircraftState.Landed;
                    }
                    break;
            }
        }
    }
}

