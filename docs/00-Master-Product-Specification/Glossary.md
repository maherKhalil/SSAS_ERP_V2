# Glossary

**Document ID:** MPS-GLS-001

**Version:** 1.0

**Status:** Approved

---

# Purpose

This glossary defines the business and technical terminology used throughout SSAS ERP.

All documents shall use the definitions contained in this glossary.

Terms shall not be redefined elsewhere.

---

# A

## Account

A General Ledger account used to classify financial transactions.

Related Modules

General Ledger

---

## API

Application Programming Interface.

REST endpoints exposed by the ERP.

---

## Audit Trail

A chronological record of business activities performed within the system.

---

## Authentication

The process of verifying the identity of a user.

---

## Authorization

The process of determining what an authenticated user is permitted to access.

---

# B

## Business Rule

A mandatory rule governing business behavior.

Business Rules are identified using the BR-* numbering scheme.

---

# C

## Company

A legal business entity operating under a Tenant.

A Tenant may own multiple Companies.

Company data is isolated from other Companies according to permissions.

---

## Configuration

Settings controlling application behavior without requiring code changes.

---

# D

## Department

An organizational unit within a Company.

Departments may be hierarchical.

---

## Document Number

A unique business identifier assigned to documents such as Journal Entries, Employees, Purchase Orders, and Invoices.

Numbering sequences are configurable per Company.

---

# E

## Employee

A person employed by a Company.

Employees belong to the HR domain.

An Employee may or may not have an application User account.

---

## Event

A business occurrence that may trigger additional processing.

Examples

Employee Created

Journal Posted

Invoice Approved

---

# F

## Feature

A functional capability provided by the application.

Features are identified using FR-* identifiers.

---

## Fiscal Period

A configurable accounting period used by the General Ledger.

---

## Fiscal Year

A collection of Fiscal Periods representing an accounting year.

---

# G

## General Ledger

The accounting module responsible for financial transactions.

---

# I

## Identity

The authenticated digital identity of a User.

---

## Integration

Communication between SSAS ERP and external systems.

---

# J

## Journal Entry

A balanced accounting transaction containing Debit and Credit lines.

---

# L

## Localization

Adaptation of the application to language, currency, date format, and regional settings.

---

# M

## Module

A self-contained functional area within the ERP.

Examples

Platform

HR

General Ledger

Payroll

Inventory

CRM

Manufacturing

---

## Multi-Tenancy

The architectural capability allowing multiple independent customers to share one application while maintaining complete logical isolation.

---

# P

## Permission

A security capability granting access to one or more operations.

Permissions are assigned through Roles.

---

## Platform

The shared infrastructure supporting all ERP modules.

Examples

Authentication

Tenant Management

Notifications

Audit

Configuration

---

## Posting

The process of making a financial transaction permanent.

Posted Journal Entries cannot be modified.

---

# R

## Requirement

A documented capability or constraint identified by a permanent Requirement ID.

---

## Role

A collection of Permissions assigned to Users.

---

# S

## Screen

A user interface page.

Screens are identified using SCR-* identifiers.

---

## Subscription

A commercial agreement determining which modules and features are available to a Tenant.

---

# T

## Tenant

The highest logical boundary within SSAS ERP.

Each Tenant owns one or more Companies.

Data is never shared between Tenants.

---

## Time Zone

A regional configuration used when presenting dates and times.

All timestamps are stored internally in UTC.

---

# U

## User

An authenticated application account.

A User may access one or more Companies depending on assigned permissions.

A User is not necessarily an Employee.

---

# W

## Workflow

A sequence of business activities performed to complete a business process.

Examples

Employee Hiring

Journal Approval

Purchase Approval

Leave Request

---

# Numbering Prefixes

| Prefix | Meaning |
|---------|---------|
| REQ | Requirement |
| BR | Business Rule |
| FR | Feature |
| SCR | Screen |
| API | REST API |
| TBL | Database Table |
| WF | Workflow |
| PER | Permission |
| TC | Test Case |
| ADR | Architecture Decision |
| RPT | Report |
| NFR | Non-Functional Requirement |

---

# Document Ownership

The Product Architecture Team is responsible for maintaining this glossary.

Changes require review to ensure consistency across all documentation.