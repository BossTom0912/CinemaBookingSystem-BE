# CinemaBookingDB Database Schema Documentation

This document provides a high-level overview of the database structure defined in `cinema-booking-schema.sql`. The database is designed for a robust online cinema ticket booking and management system.

## 1. Authentication and Authorization
- **ROLE**: Stores system roles (Customer, Staff, Manager, Admin).
- **USER**: Core authentication table storing login credentials and account status.
- **CUSTOMER_PROFILE**: Extended profile for customers, tracking member levels and reward points.
- **STAFF_PROFILE**: Extended profile for employees, tracking positions and assigned cinemas.
- **EMAIL_VERIFICATION_TOKEN**: Stores OTP tokens for email verification, password reset, and info updates.
- **REFRESH_TOKEN**: Manages user sessions securely using hashed refresh tokens.

## 2. Cinema Structure
- **CINEMA**: Physical theater locations.
- **ROOM**: Screening rooms inside a cinema.
- **SEAT_TYPE**: Categories of seats (Standard, VIP, Sweetbox) and their extra fees.
- **SEAT**: Specific seats mapped inside a room.

## 3. Movie and Catalog Management
- **MOVIE**: Movie details, classifications, and aggregated analytics (views, ratings).
- **GENRE**: Available movie genres.
- **MOVIE_GENRE**: Mapping table connecting movies to multiple genres.
- **LANGUAGE**: Available languages for dubbing and subtitles.
- **FB_ITEM**: Food & Beverage items (e.g., Popcorn, Drinks).
- **CINEMA_FB_INVENTORY**: Inventory tracking of F&B items per cinema.

## 4. Showtimes and Seat Tracking
- **SHOWTIME**: Scheduled movie screenings.
- **SHOWTIME_SEAT**: Real-time tracking of seat statuses (Available, Locked, Booked) for each showtime.

## 5. Booking and Ticketing
- **BOOKING**: Orders placed by customers (Online) or staff (Counter). Supports guest checkouts.
- **BOOKING_SEAT**: Specific seats reserved within a booking.
- **BOOKING_FB_ITEM**: F&B items purchased alongside tickets.
- **TICKET**: Generated digital tickets (QR Codes) linked to specific booked seats.
- **CHECKIN_LOG**: Audit log of ticket scanning at the cinema.

## 6. Payment, Cancellation, and Refund
- **PAYMENT_PROVIDER**: Supported payment gateways (e.g., SePay, VNPay).
- **PAYMENT**: Transaction records for bookings, handling callbacks and status.
- **SHOWTIME_CANCELLATION**: Records of showtimes canceled by Admins/Managers.
- **REFUND**: Tracks the refund process for canceled showtimes or failed bookings.

## 7. Promotions and Vouchers
- **VOUCHER**: Discount campaigns with limits and expiration dates.
- **VOUCHER_USAGE**: Audit trail mapping which customer used which voucher on which booking.

## 8. Social and Interaction
- **REVIEW**: User feedback and ratings for movies.
- **REVIEW_EDIT_HISTORY**: Tracks changes to user reviews over time.
- **REVIEW_MODERATION_HISTORY**: Moderation logs by AI or staff for flagged reviews.
- **CHAT_HISTORY**: Stores conversation logs between users and the AI assistant.

## 9. Analytics and Tracking
- **REWARD_POINT_TRANSACTION**: Tracks earning, redeeming, or adjusting customer loyalty points.
- **MOVIE_VIEW_LOG**: Detailed log of user interactions/views with movies.
- **MOVIE_DAILY_VIEW**: Aggregated daily view counts per movie for analytics.
- **AUDIT_LOG**: System-wide logging for administrative actions.
- **NOTIFICATION**: In-app notifications sent to users.

---
**Note:** For the complete DDL structure, constraints, indexes, and test seed data, refer to `cinema-booking-schema.sql`. Do not duplicate raw SQL code here to keep the documentation clean and maintainable.
