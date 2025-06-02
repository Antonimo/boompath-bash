# Boompath Bash - Project Decisions & Development Philosophy

// TODO: review

## Overview

This document captures key project-wide decisions, development priorities, and conscious trade-offs made during development. These decisions help maintain consistency and provide context for future development work.

## Current Development Phase: MVP Focus

### Countdown & Lobby System Decisions

#### **Immediate UI Feedback Priority**

- **Decision**: Keep immediate broadcast for ready state changes to provide instant player feedback
- **Rationale**: UX is critical - players should see immediate response when they click "Ready"
- **Implementation**: `LobbyManager.ToggleLocalPlayerReadyState()` calls `BroadcastLobbyState()` immediately after successful UGS update

#### **Simplified Countdown Logic**

- **Decision**: Assume nothing will cause countdown to stop once all players are ready
- **Rationale**: At this development stage, focusing on core functionality rather than edge cases
- **Implementation**:
  - Ready button is disabled after clicking (prevents "unready" actions)
  - Countdown start decision should be made on the definitive event (player ready state change)
  - No complex countdown cancellation logic needed

#### **Edge Case Handling Strategy**

- **Decision**: Do not programmatically handle disconnection/network edge cases during countdown
- **Rationale**: Conscious trade-off between development effort and UX at MVP stage
- **User Experience**: If disconnection occurs, users can restart the game (acceptable compromise)
- **Future**: Can be enhanced later as the project matures

### Network Event Flow Philosophy

#### **Event-Driven Architecture with UX Optimizations**

- **Approach**: Hybrid model combining immediate local feedback with event-driven consistency
- **Pattern**:
  1. Immediate local state update for UX
  2. Asynchronous UGS event confirmation for network consistency
  3. Smart deduplication to prevent race conditions

#### **Race Condition Handling**

- **Issue**: UGS events arrive after immediate broadcasts, causing false countdown cancellations
- **Solution**: Make countdown decisions on the "last word" event rather than aggressive cancellation
- **Implementation**: `HandlePlayerReadyStateChanged` should check actual state rather than assume state degradation

## Code Organization Principles

### **Single Responsibility & Component Separation**

- **PrivateMatchManager**: High-level match orchestration and coordination
- **LobbyManager**: UGS lobby operations and state management
- **NetworkGameManager**: Network-specific game flow management
- **GameManager**: Local game mechanics and interactions

### **Event Broadcasting Strategy**

- **Centralized Events**: Use LobbyManager as single source of truth for lobby state
- **Immediate Feedback**: Broadcast optimistic updates for UX
- **Eventual Consistency**: Rely on UGS events for network synchronization

## Development Priorities (Current Phase)

1. **Core Functionality First**: Focus on main game flow working reliably
2. **UX Over Edge Cases**: Prioritize good user experience in normal cases
3. **Simplicity Over Robustness**: Accept some limitations for faster development
4. **Iterative Enhancement**: Plan to revisit and enhance systems as project grows

## Known Limitations & Future Enhancements

### **Current Limitations**

- No handling of player disconnection during countdown
- No recovery from network issues during match setup
- Limited error handling for edge cases

### **Future Enhancement Areas**

- Reconnection handling
- Robust error recovery
- More sophisticated countdown cancellation logic
- Player state persistence across network issues

## Architecture Notes

### **Unity & UGS Integration**

- Using Unity Game Services (UGS) Lobby for multiplayer coordination
- Event-driven updates from UGS with local optimistic updates for UX
- Unity Netcode for actual game networking

### **Mobile-First Design**

- Optimized for mobile performance
- Touch-friendly UI design
- Minimal garbage collection patterns
