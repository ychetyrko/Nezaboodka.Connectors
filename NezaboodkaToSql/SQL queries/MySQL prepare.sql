/*********************************************

	Create Nezaboodka administrative database
		and grant all rights to nezaboodka user for it
        
**********************************************/

CREATE DATABASE IF NOT EXISTS `nz_admin_db`;
USE `nz_admin_db`;

/*********************************************

	Prepare administrative database tables
        
**********************************************/

/*********************************************
	Databases list to store user rights:
		0 - ReadWrite
		1 - ReadOnly
        2 - NoAccess
*/
DROP TABLE IF EXISTS `db_list`;
CREATE TABLE `db_list`(
	`name` VARCHAR(64) PRIMARY KEY NOT NULL UNIQUE,
	`access` TINYINT DEFAULT 0
    /* set 'ReadWrite' access by default for any new database */
);

/*********************************************
	List of databases to add
*/
DROP TABLE IF EXISTS `db_add_list`;
CREATE TABLE `db_add_list`(
	`name` VARCHAR(64) NOT NULL UNIQUE
);

/*********************************************
	List of databases to remove
*/
DROP TABLE IF EXISTS `db_rem_list`;
CREATE TABLE `db_rem_list`(
	`name` VARCHAR(64) NOT NULL UNIQUE
);


/*********************************************

		Stored procedures and functions
        
**********************************************/

DELIMITER //

/*********************************************
	Prepare empty database for work
*/
DROP PROCEDURE IF EXISTS prepare_db //
CREATE PROCEDURE prepare_db(db_name VARCHAR(64))
BEGIN
	# TODO: create `type` and `field` tables
END //


/*********************************************
	Effectively alter database list
*/
DROP PROCEDURE IF EXISTS remove_databases //
CREATE PROCEDURE remove_databases ()
BEGIN
	DECLARE done INT DEFAULT FALSE;
    DECLARE db_name VARCHAR(64);
    DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_rem_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
    
    OPEN cur;
    
    proc_loop: LOOP
		FETCH cur INTO db_name;
        IF done THEN
			LEAVE proc_loop;
		END IF;
		
        SET @prepStr=CONCAT('DROP DATABASE IF EXISTS ', db_name, ';');
		PREPARE procPrep FROM @prepStr;
		EXECUTE procPrep;
		DEALLOCATE PREPARE procPrep;
        
        DELETE FROM db_list
        WHERE `name` = db_name;
    END LOOP;
    
    CLOSE cur;
	
    TRUNCATE TABLE db_rem_list;
END //

DROP PROCEDURE IF EXISTS add_databases //
CREATE PROCEDURE add_databases ()
BEGIN
	DECLARE done INT DEFAULT FALSE;
    DECLARE db_name VARCHAR(64);
    DECLARE db_exists INT DEFAULT 0;
    DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_add_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
    
    OPEN cur;
    
    proc_loop: LOOP
		FETCH cur INTO db_name;
        IF done THEN
			LEAVE proc_loop;
		END IF;
        
		SET @prepStr=CONCAT('CREATE DATABASE ', db_name, ';');
        PREPARE procPrep FROM @prepStr;
        EXECUTE procPrep;
        DEALLOCATE PREPARE procPrep;
            
        SET @prepStr=CONCAT('INSERT INTO `db_list` (`name`) value (\'', db_name, '\');');
        PREPARE procPrep FROM @prepStr;
        EXECUTE procPrep;
        DEALLOCATE PREPARE procPrep;
        
        CALL prepare_db(db_name);
    END LOOP;
    
    CLOSE cur;
	
    TRUNCATE TABLE db_add_list;
END //

DROP PROCEDURE IF EXISTS alter_database_list //
CREATE PROCEDURE alter_database_list ()
BEGIN
	CALL remove_databases();
    CALL add_databases();
    
    SELECT `name` FROM `db_list`;
END //

/*
	Protocol for altering database list:
    
	1. fill `db_rem_list` and `db_add_list` tables with database names;
	2. call `alter_database_list`
		(or add_databases / remove_databases separately)
	
**********************************************/

/*********************************************
	TODO: other stored procedures...
*/

DELIMITER ;

/******************************************************************************/

/*********************************************

		Create Nezaboodka users
			and grant rights for databases
        
**********************************************/

CREATE USER `nz_admin`@'%' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'%';

/*	Localhost user	*/
CREATE USER `nz_admin`@'localhost' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'localhost';

FLUSH PRIVILEGES;
