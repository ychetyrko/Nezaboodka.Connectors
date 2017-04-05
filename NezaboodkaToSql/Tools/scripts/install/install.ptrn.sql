/* ********************************************

	Create Nezaboodka administrative database
			and nezaboodka users

********************************************* */

CREATE DATABASE `nz_admin_db` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_BIN`;
USE `nz_admin_db`;


/* ********************************************

		Administrative database tables
*/

#include "tables/database list.sql"


/* ********************************************

			Stored procedures
*/

#include "procedures/qexec.sql"

#include "procedures/prepare new database.ptrn.sql"

#include "procedures/alter database list.sql"

#include "procedures/alter database schema.ptrn.sql"


/* ********************************************

		Create Nezaboodka users
			and grant rights for databases

********************************************* */

CREATE USER `nz_admin`@'%' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'%';

-- Localhost user
CREATE USER `nz_admin`@'localhost' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'localhost';

FLUSH PRIVILEGES;
